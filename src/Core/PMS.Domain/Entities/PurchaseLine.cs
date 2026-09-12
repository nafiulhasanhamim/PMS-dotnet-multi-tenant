using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;

namespace PMS.Domain.Entities;

/// <summary>
/// One product, out of one batch, on one purchase.
///
/// <para><b>These columns deliberately duplicate the batch's.</b> A batch records what is on the
/// shelf <em>now</em>: its quantity falls as stock sells, and an adjustment can move it in either
/// direction. This line records what the supplier delivered and invoiced on the day, and that
/// figure must never move — it is what the bill said, it is what the balance was computed from,
/// and it is what somebody checks a disputed invoice against six months later.</para>
///
/// <para>Reading the quantity off the batch instead would mean a purchase's total silently
/// changing every time a customer bought something out of that delivery, which is the single most
/// destructive thing this module could do to a supplier's account.</para>
/// </summary>
public sealed class PurchaseLine : BaseAuditableAggregateRoot<Guid>, ITenantEntity
{
    private readonly List<PurchaseReturn> _returns = [];

    // EF materialises through this.
    private PurchaseLine()
    {
    }

    /// <summary>
    /// Internal: only <see cref="Purchase.AddLine"/> constructs one, after the batch it points at
    /// has been created.
    /// </summary>
    internal PurchaseLine(
        Guid purchaseId,
        Guid productId,
        Guid batchId,
        int quantityInBaseUnits,
        decimal purchasePricePerBaseUnit)
    {
        Id = Guid.NewGuid();
        PurchaseId = purchaseId;
        ProductId = productId;
        BatchId = batchId;
        QuantityInBaseUnits = quantityInBaseUnits;
        PurchasePricePerBaseUnit = purchasePricePerBaseUnit;
        LineTotal = quantityInBaseUnits * purchasePricePerBaseUnit;
    }

    /// <summary>Stamped by the persistence interceptor on insert; never set by a handler.</summary>
    public Guid TenantId { get; set; }

    public Guid PurchaseId { get; private set; }

    public Purchase Purchase { get; private set; } = null!;

    public Guid ProductId { get; private set; }

    public Product Product { get; private set; } = null!;

    /// <summary>
    /// The batch this delivery became.
    ///
    /// <para>Set at construction, after the batch exists. It is also what makes "return this to
    /// the supplier" possible from the expired-stock screen: a batch with a purchase line behind
    /// it can go back, and one entered through Add Stock has nobody to send it to.</para>
    /// </summary>
    public Guid BatchId { get; private set; }

    public Batch Batch { get; private set; } = null!;

    /// <summary>What was delivered, in base units. Frozen — see the class remarks.</summary>
    public int QuantityInBaseUnits { get; private set; }

    /// <summary>
    /// What it cost per base unit. <c>DECIMAL(18,4)</c>, like every other per-unit price in the
    /// system, because a price derived from a pack is routinely fractional.
    /// </summary>
    public decimal PurchasePricePerBaseUnit { get; private set; }

    /// <summary>
    /// Quantity × price. Stored rather than computed on read so that the purchase's total and the
    /// lines that make it up cannot disagree, even if the arithmetic were ever changed.
    /// </summary>
    public decimal LineTotal { get; private set; }

    /// <summary>
    /// What has already gone back against this line.
    ///
    /// <para>Mapped so a return can be decided from one loaded aggregate rather than a second
    /// query — the same reason <c>SaleLine</c> maps its own. A handler that forgot to load these
    /// would read zero returned, silently, and allow the same goods to be sent back twice.</para>
    /// </summary>
    public IReadOnlyCollection<PurchaseReturn> Returns => _returns;

    /// <summary>How much of this line has gone back, in base units.</summary>
    public int ReturnedInBaseUnits => _returns.Sum(r => r.QuantityInBaseUnits);

    /// <summary>What those returns credited.</summary>
    public decimal ReturnedAmount => _returns.Sum(r => r.ReturnAmount);
}
