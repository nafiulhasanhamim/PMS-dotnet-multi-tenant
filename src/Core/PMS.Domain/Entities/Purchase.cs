using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;

namespace PMS.Domain.Entities;

/// <summary>
/// One delivery from one supplier, against one bill.
///
/// <para><b>A purchase creates batches; it does not replace them.</b> Each line calls Module 3's
/// batch-creation command, so the stock that arrives is the same stock the shelf, FEFO, expiry
/// alerts and billing already understand. What this adds on top is the money: who it came from,
/// what the bill said, and what remains unpaid. The standalone "Add stock" screen stays for
/// opening inventory, free samples and corrections — deliveries with no bill behind them.</para>
///
/// <para><b>A purchase cannot be edited or deleted once recorded.</b> It is a statement about what
/// a supplier delivered and invoiced, and both the stock and the balance have already moved on
/// that basis. Editing the total would silently restate a debt; deleting it would orphan batches
/// that have since been sold from. The corrective paths are a purchase return (goods going back)
/// and a stock adjustment (a counting error) — both of which leave a record of the correction
/// rather than quietly replacing the original.</para>
///
/// <para><b>Built lines-first.</b> <see cref="AddLine"/> may only be called before
/// <see cref="Settle"/>, which computes and freezes <see cref="TotalAmount"/>. The order is
/// enforced rather than documented, for the same reason <c>Sale</c> enforces its own: a total
/// that was computed before the last line was added is wrong in a way nothing later detects.</para>
/// </summary>
public sealed class Purchase : BaseAuditableAggregateRoot<Guid>, ITenantEntity
{
    private readonly List<PurchaseLine> _lines = [];

    /// <summary>
    /// Guards the build order during construction only.
    ///
    /// <para>A field rather than a property so EF cannot discover and map it: it describes a
    /// moment in this object's construction, not a fact about the row. A purchase read back from
    /// the database has it false, which is harmless — nothing adds lines to a purchase that has
    /// already been recorded, because a recorded purchase cannot be edited at all.</para>
    /// </summary>
    private bool _settled;

    // EF materialises through this.
    private Purchase()
    {
    }

    /// <summary>
    /// Starts a purchase. Lines follow, then <see cref="Settle"/> — see the class remarks.
    /// </summary>
    public Purchase(
        Guid supplierId,
        string purchaseNumber,
        DateOnly purchaseDate,
        Guid createdByUserId,
        string? notes)
    {
        Id = Guid.NewGuid();
        SupplierId = supplierId;
        PurchaseNumber = purchaseNumber;
        PurchaseDate = purchaseDate;
        CreatedByUserId = createdByUserId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        AmountPaid = 0m;
    }

    /// <summary>Stamped by the persistence interceptor on insert; never set by a handler.</summary>
    public Guid TenantId { get; set; }

    public Guid SupplierId { get; private set; }

    public Supplier Supplier { get; private set; } = null!;

    /// <summary>
    /// "PUR-000123". Unique per pharmacy, never across them — two pharmacies both have a
    /// PUR-000001, which is why every unique index in this module leads with TenantId.
    /// </summary>
    public string PurchaseNumber { get; private set; } = null!;

    /// <summary>
    /// The date on the supplier's bill, which is not necessarily today.
    ///
    /// <para>A <c>DateOnly</c>, unlike a sale's timestamp. A sale happens at a moment somebody
    /// can be asked about; a delivery is booked in against a day, often the day after it
    /// physically arrived, and a time of day would be invented precision.</para>
    /// </summary>
    public DateOnly PurchaseDate { get; private set; }

    /// <summary>
    /// The sum of the line totals, computed by <see cref="Settle"/> and stored.
    ///
    /// <para>Stored rather than recomputed, and the distinction from the supplier balance is
    /// deliberate: <em>this</em> is a fact about a bill that was issued once and does not change,
    /// whereas the balance moves every time a payment or return happens. A figure that cannot
    /// change is safe to store; one that can is not.</para>
    /// </summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>
    /// What has been paid <em>against this purchase specifically</em>.
    ///
    /// <para><b>General payments do not touch this.</b> A payment recorded against the supplier
    /// rather than a bill reduces what they are owed overall but says nothing about which bill it
    /// settled — inventing an allocation and writing it here would turn a display convenience into
    /// a stored fact. The supplier's balance accounts for those; this column does not.</para>
    /// </summary>
    public decimal AmountPaid { get; private set; }

    public string? Notes { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public IReadOnlyCollection<PurchaseLine> Lines => _lines;

    /// <summary>
    /// Adds a line for a batch that has already been created.
    ///
    /// <para>The batch comes first because the line references it, and because a line for a batch
    /// that failed to be created should not exist at all. The whole sequence runs in one
    /// transaction, so a failure on the third line takes the first two batches with it.</para>
    /// </summary>
    /// <exception cref="InvalidOperationException">The purchase has already been settled.</exception>
    public PurchaseLine AddLine(
        Guid productId, Guid batchId, int quantityInBaseUnits, decimal purchasePricePerBaseUnit)
    {
        if (_settled)
        {
            throw new InvalidOperationException(
                "Lines cannot be added after the purchase total has been computed.");
        }

        var line = new PurchaseLine(
            Id, productId, batchId, quantityInBaseUnits, purchasePricePerBaseUnit);

        _lines.Add(line);

        return line;
    }

    /// <summary>
    /// Freezes the total. Called once, after every line is in.
    /// </summary>
    /// <exception cref="InvalidOperationException">No lines, or already settled.</exception>
    public void Settle()
    {
        if (_settled)
        {
            throw new InvalidOperationException("This purchase has already been settled.");
        }

        if (_lines.Count == 0)
        {
            throw new InvalidOperationException(
                "A purchase must have at least one line. An empty delivery is not a purchase.");
        }

        TotalAmount = _lines.Sum(line => line.LineTotal);
        _settled = true;
    }

    /// <summary>
    /// Records a payment made against this bill. Called only for a purchase-linked payment.
    ///
    /// <para><b>Not capped at the total.</b> Overpaying a bill is a real thing that happens —
    /// a rounded-up cash settlement, or a payment entered against the wrong purchase — and
    /// refusing it here would leave somebody unable to record money that has genuinely left the
    /// till. The API warns; it does not block. The supplier's balance goes negative, and every
    /// screen that shows a balance is written to say so rather than clamp at zero.</para>
    /// </summary>
    public void ApplyPayment(decimal amount) => AmountPaid += amount;
}
