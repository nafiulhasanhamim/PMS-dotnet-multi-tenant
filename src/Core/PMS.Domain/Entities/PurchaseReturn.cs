using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;

namespace PMS.Domain.Entities;

/// <summary>
/// Goods going back to the supplier, against one line of one purchase.
///
/// <para><b>It targets a line, not a purchase.</b> A delivery of six products is returned one
/// product at a time — near expiry on one, damaged on another — and the amount credited depends
/// on what that specific line cost. A return against a whole purchase would have to invent an
/// allocation across its lines, and would make "return the two damaged boxes" impossible to
/// express at all.</para>
///
/// <para><b>Two things move, and they move for different reasons.</b> The stock goes down, through
/// a <c>StockAdjustment</c> like every other non-sale quantity change in the system — so the shelf,
/// FEFO and the alerts all stay true without knowing this module exists. And the supplier's
/// balance goes down, because goods that went back are not goods that were bought.</para>
///
/// <para><b>What does not move is <c>Purchase.AmountPaid</c>.</b> A return does not un-pay money
/// that has already been handed over; it reduces what is still owed. If the pharmacy had already
/// paid the bill in full, the return pushes the balance negative — the supplier now owes them —
/// and that is the truthful answer rather than an error.</para>
/// </summary>
public sealed class PurchaseReturn : BaseAuditableAggregateRoot<Guid>, ITenantEntity
{
    // EF materialises through this.
    private PurchaseReturn()
    {
    }

    /// <param name="purchasePricePerBaseUnit">
    /// <b>The batch's cost, which is also the line's.</b> The two are equal by construction —
    /// the batch was created from this line — and taking it from the batch is what the brief
    /// specifies, so a return credits exactly what the goods were invoiced at.
    /// </param>
    public PurchaseReturn(
        Guid purchaseLineId,
        Guid batchId,
        int quantityInBaseUnits,
        decimal purchasePricePerBaseUnit,
        string reason,
        Guid returnedByUserId)
    {
        Id = Guid.NewGuid();
        PurchaseLineId = purchaseLineId;
        BatchId = batchId;
        QuantityInBaseUnits = quantityInBaseUnits;
        ReturnAmount = quantityInBaseUnits * purchasePricePerBaseUnit;
        Reason = reason.Trim();
        ReturnedByUserId = returnedByUserId;
    }

    /// <summary>Stamped by the persistence interceptor on insert; never set by a handler.</summary>
    public Guid TenantId { get; set; }

    public Guid PurchaseLineId { get; private set; }

    public PurchaseLine PurchaseLine { get; private set; } = null!;

    /// <summary>
    /// The batch the stock came out of. Always the line's own batch — the handler checks, and the
    /// check is worth having because a mismatch would take stock off one batch while crediting the
    /// cost of another.
    /// </summary>
    public Guid BatchId { get; private set; }

    public Batch Batch { get; private set; } = null!;

    public int QuantityInBaseUnits { get; private set; }

    /// <summary>Required. Prefixed onto the stock adjustment's reason, so the two read as one event.</summary>
    public string Reason { get; private set; } = null!;

    /// <summary>Quantity × the cost per base unit. What comes off the supplier's balance.</summary>
    public decimal ReturnAmount { get; private set; }

    public Guid ReturnedByUserId { get; private set; }
}
