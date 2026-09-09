using PMS.Domain.Enums;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;

namespace PMS.Domain.Entities;

/// <summary>
/// One delivery of one product: a quantity that arrived together, with its own expiry date and
/// its own cost.
///
/// <para><b>Why cost and expiry live here and not on the product.</b> A pharmacy holds Napa
/// 500 bought in March at ৳0.80 expiring next January and more of it bought in July at ৳0.85
/// expiring the following June. Those are two different physical things on the shelf. Putting
/// either figure on the product would force a single answer to "what did this cost?" and
/// "when does it expire?", and both answers would be wrong: margin would be computed against
/// whichever price was entered last, and the expiry alert would either cry wolf about stock
/// that is fine or stay silent about stock that is not.</para>
///
/// <para><b>Quantities are integer base units.</b> Pieces for tablets, bottles for handwash,
/// bags for saline. Nothing in this entity knows which — the unit vocabulary belongs to the
/// product, and the conversion from what a person typed ("2 cartons") happens in the
/// application layer before it reaches here.</para>
///
/// <para><b>Quantity changes only through <see cref="Adjust"/>.</b> There is no setter and no
/// other method that touches it. See <see cref="StockAdjustment"/> for why that matters and
/// how the audit row is made unavoidable rather than merely expected.</para>
/// </summary>
public sealed class Batch : BaseAuditableAggregateRoot<Guid>, ITenantEntity
{
    private readonly List<StockAdjustment> _adjustments = [];

    // EF materialises through this.
    private Batch()
    {
    }

    /// <summary>
    /// Records a delivery. Validation — expiry required for a medicine, expiry not in the
    /// past, quantity positive — happens in the application layer, where a failure becomes a
    /// field error on a form rather than an exception.
    /// </summary>
    public Batch(
        Guid productId,
        string batchNumber,
        DateOnly? expiryDate,
        DateOnly? manufactureDate,
        decimal purchasePricePerBaseUnit,
        int quantityInBaseUnits,
        Guid? supplierId,
        string? supplierNameText,
        string? notes)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        BatchNumber = batchNumber.Trim();
        ExpiryDate = expiryDate;
        ManufactureDate = manufactureDate;
        PurchasePricePerBaseUnit = purchasePricePerBaseUnit;

        // Both set from the same argument, once. This is the only moment at which the two are
        // allowed to be equal by assignment; from here they diverge as stock moves.
        QuantityInBaseUnits = quantityInBaseUnits;
        InitialQuantityInBaseUnits = quantityInBaseUnits;

        SupplierId = supplierId;
        SupplierNameText = Blank(supplierNameText);
        Notes = Blank(notes);
        IsActive = true;
    }

    /// <summary>Stamped by the persistence interceptor on insert; never set by a handler.</summary>
    public Guid TenantId { get; set; }

    public Guid ProductId { get; private set; }

    /// <summary>The product this is stock of. Mapped: nearly every read needs its unit names.</summary>
    public Product Product { get; private set; } = null!;

    /// <summary>
    /// The manufacturer's batch number, as printed on the pack. Not generated here — it is
    /// how a recall notice identifies stock, so it has to be what the pack says.
    /// </summary>
    public string BatchNumber { get; private set; } = null!;

    /// <summary>
    /// When this stock expires, or null for stock that does not.
    ///
    /// <para>Nullable because diapers, syringes and bandages genuinely have no expiry, and a
    /// mandatory column would be filled with an invented date — which is worse than no date,
    /// because it silently drives the expiry alerts. The requirement is conditional on the
    /// product type and lives in validation rather than the schema: the rule depends on
    /// another table's column, which a CHECK constraint cannot see.</para>
    /// </summary>
    public DateOnly? ExpiryDate { get; private set; }

    public DateOnly? ManufactureDate { get; private set; }

    /// <summary>
    /// What one base unit of this batch cost. Decimal, and unrounded — a carton of 24 at
    /// ৳4,320 is ৳180 a bottle, but a strip of 3 at ৳10 is ৳3.3333…, and rounding that here
    /// would misstate margin on every sale of it.
    /// </summary>
    public decimal PurchasePricePerBaseUnit { get; private set; }

    /// <summary>What the batch holds now, in base units. Never negative; see <see cref="Adjust"/>.</summary>
    public int QuantityInBaseUnits { get; private set; }

    /// <summary>
    /// What the batch held when it was created. Set once, in the constructor, and never
    /// touched again by anything.
    ///
    /// <para><b>Why it exists.</b> It is the denominator. Turnover, wastage as a percentage of
    /// a delivery, and "did this batch sell before it expired" all need to know how much
    /// arrived, and the live quantity has by then been reduced by every sale and write-off.
    /// It is also the sanity check that catches a bug elsewhere: the live quantity should
    /// always equal this plus the sum of the batch's adjustments minus what was sold, and
    /// nothing else can be true.</para>
    ///
    /// <para><b>Why nothing may modify it.</b> Stock arriving later is a new batch, not a
    /// bigger old one — it has its own expiry and its own cost. Adding to this figure instead
    /// would blend two deliveries into one row and destroy both.</para>
    /// </summary>
    public int InitialQuantityInBaseUnits { get; private set; }

    /// <summary>
    /// Who supplied it. No foreign key yet — the Supplier entity arrives in Module 4 and the
    /// column is here so that migration adds a constraint rather than a column.
    /// </summary>
    public Guid? SupplierId { get; private set; }

    /// <summary>
    /// The supplier's name as free text, until <see cref="SupplierId"/> becomes real. Kept
    /// after that too: it is what was written on the delivery note, which is worth preserving
    /// even once the same supplier has a record of its own.
    /// </summary>
    public string? SupplierNameText { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>
    /// Whether this batch row is in use.
    ///
    /// <para><b>Not depletion.</b> A batch that has sold out stays active and stays visible —
    /// its cost and expiry are the history behind sales that have already happened. This flag
    /// is for a row entered in error, and it is the reason such a row can be hidden without
    /// being deleted.</para>
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>This batch's adjustment history, oldest first.</summary>
    public IReadOnlyCollection<StockAdjustment> Adjustments => _adjustments;

    /// <summary>Whether any stock is left. Depleted batches are excluded from FEFO, not hidden.</summary>
    public bool IsDepleted => QuantityInBaseUnits <= 0;

    /// <summary>Whether this batch has expired as at <paramref name="today"/> (a UTC date).</summary>
    public bool IsExpiredAsOf(DateOnly today) => ExpiryDate is { } expiry && expiry < today;

    /// <summary>
    /// Days until expiry as at <paramref name="today"/>, or null for stock that does not
    /// expire. Negative once expired, which is what lets one comparison serve both the
    /// "expired" and "expiring soon" cases.
    /// </summary>
    public int? DaysUntilExpiry(DateOnly today) =>
        ExpiryDate is { } expiry ? expiry.DayNumber - today.DayNumber : null;

    /// <summary>
    /// Applies a quantity change and returns the audit row that records it.
    ///
    /// <para><b>This is the only way a batch's quantity changes.</b> The adjustment is
    /// constructed here and handed back rather than left to the caller, so there is no
    /// sequence of calls that produces a quantity change without its reason. The caller's job
    /// is to persist both in one transaction.</para>
    ///
    /// <para><see cref="InitialQuantityInBaseUnits"/> is not touched, by any adjustment type,
    /// ever.</para>
    /// </summary>
    /// <param name="deltaInBaseUnits">
    /// Signed change in base units. A <see cref="AdjustmentType.Correction"/> arrives here as
    /// a delta too: the screen collects the true total and the handler subtracts, because
    /// someone holding a counted figure should not have to do the arithmetic.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The change would take the batch below zero. Thrown rather than clamped: a request to
    /// remove more than exists means the caller's idea of the quantity is stale, and quietly
    /// removing what there is would make the resulting numbers look deliberate. The
    /// application layer checks first so this surfaces as a field error, and the database
    /// carries a CHECK constraint as the last line — this is the middle one.
    /// </exception>
    public StockAdjustment Adjust(
        AdjustmentType adjustmentType,
        int deltaInBaseUnits,
        string reason,
        Guid adjustedByUserId)
    {
        var newQuantity = QuantityInBaseUnits + deltaInBaseUnits;

        if (newQuantity < 0)
        {
            throw new InvalidOperationException(
                $"Batch '{BatchNumber}' holds {QuantityInBaseUnits} base units, so it cannot "
                + $"change by {deltaInBaseUnits}. Stock cannot go negative.");
        }

        QuantityInBaseUnits = newQuantity;

        var adjustment = new StockAdjustment(
            Id, adjustmentType, deltaInBaseUnits, newQuantity, reason, adjustedByUserId);

        _adjustments.Add(adjustment);

        return adjustment;
    }

    /// <summary>
    /// Applies an edit to the batch's details. <b>Quantity is not a parameter</b> — see
    /// <see cref="Adjust"/>. Callers validate first; this trusts its input.
    /// </summary>
    public void UpdateDetails(
        string batchNumber,
        DateOnly? expiryDate,
        DateOnly? manufactureDate,
        decimal purchasePricePerBaseUnit,
        Guid? supplierId,
        string? supplierNameText,
        string? notes)
    {
        BatchNumber = batchNumber.Trim();
        ExpiryDate = expiryDate;
        ManufactureDate = manufactureDate;
        PurchasePricePerBaseUnit = purchasePricePerBaseUnit;
        SupplierId = supplierId;
        SupplierNameText = Blank(supplierNameText);
        Notes = Blank(notes);
    }

    /// <summary>Hides a row entered in error. Not for depletion.</summary>
    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
