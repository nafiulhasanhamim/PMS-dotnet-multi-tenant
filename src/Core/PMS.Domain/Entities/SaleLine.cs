using PMS.Domain.Enums;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;

namespace PMS.Domain.Entities;

/// <summary>
/// One product, out of one batch, on one sale.
///
/// <para><b>A line is per batch, not per cart row.</b> Asking for 40 tablets when the
/// soonest-expiring batch holds 30 produces two of these. The customer's invoice groups them
/// back into a single visible row — see the invoice grouping in the read model — because which
/// batch a tablet came from is the pharmacy's bookkeeping, not something the customer asked
/// about. What the split buys is a return that goes back to the right batch and a profit figure
/// that uses the right purchase cost, since the two batches may have cost different amounts.</para>
///
/// <para><b>Three columns here are snapshots, and all three have to be.</b>
/// <see cref="UnitSalePrice"/> is copied from the product at sale time, so re-pricing a
/// product next month cannot rewrite last month's invoices or last month's profit.
/// <see cref="LineTotal"/> and <see cref="NetLineTotal"/> are stored rather than derived
/// because the arithmetic that produced them involves a rounding reconciliation across the
/// whole sale — recomputing one line in isolation cannot reproduce it, and would disagree with
/// the invoice the customer is holding.</para>
/// </summary>
public sealed class SaleLine : BaseAuditableAggregateRoot<Guid>, ITenantEntity
{
    private readonly List<SalesReturn> _returns = [];

    // EF materialises through this.
    private SaleLine()
    {
    }

    /// <summary>
    /// Records one batch's contribution to a cart item. Internal: only
    /// <see cref="Sale.AddLine"/> may build one, so a line cannot exist without the sale that
    /// explains it, and the discount split cannot miss a line it never saw.
    /// </summary>
    internal SaleLine(
        Guid saleId,
        Guid productId,
        Guid batchId,
        int quantityInBaseUnits,
        UnitLevel unitSold,
        decimal unitSalePrice,
        decimal lineTotal)
    {
        Id = Guid.NewGuid();
        SaleId = saleId;
        ProductId = productId;
        BatchId = batchId;
        QuantityInBaseUnits = quantityInBaseUnits;
        UnitSold = unitSold;
        UnitSalePrice = unitSalePrice;
        LineTotal = lineTotal;

        // Until the sale's discount is applied. A sale with no discount leaves them here,
        // which is correct: the share is zero and the net is the line total.
        DiscountShare = 0m;
        NetLineTotal = lineTotal;
    }

    /// <summary>Stamped by the persistence interceptor on insert; never set by a handler.</summary>
    public Guid TenantId { get; set; }

    public Guid SaleId { get; private set; }

    public Guid ProductId { get; private set; }

    /// <summary>
    /// The batch this line took stock out of.
    ///
    /// <para>Required, and the reason a return can be honest: it reverses <em>this</em>
    /// deduction rather than guessing. Physically nobody checks the sticker on a returned pack,
    /// so this is a bookkeeping convention — but it is the convention that keeps expiry dates
    /// and purchase costs attached to the right stock.</para>
    /// </summary>
    public Guid BatchId { get; private set; }

    /// <summary>
    /// How much left the batch, in base units — always an integer, because that is how stock
    /// is held. A split line can hold a quantity that is not a whole number of
    /// <see cref="UnitSold"/>: 5 pieces of a 3-per-strip product may split 4 and 1.
    /// </summary>
    public int QuantityInBaseUnits { get; private set; }

    /// <summary>
    /// The level the customer actually bought at, for the invoice to read "2 strips" rather
    /// than "6 pieces". Purely presentational; every calculation runs on base units.
    /// </summary>
    public UnitLevel UnitSold { get; private set; }

    /// <summary>
    /// Price per <see cref="UnitSold"/> as it stood when the sale completed. <b>The snapshot.</b>
    /// Never re-read from the product to display or report on a past sale.
    /// </summary>
    public decimal UnitSalePrice { get; private set; }

    /// <summary>What this line came to before any discount.</summary>
    public decimal LineTotal { get; private set; }

    /// <summary>
    /// This line's proportional share of the bill's discount, in taka. Summed across the
    /// sale's lines this equals <see cref="Sale.DiscountAmount"/> exactly — see
    /// <c>SaleMath.SplitDiscount</c> for the residual rule that guarantees it.
    /// </summary>
    public decimal DiscountShare { get; private set; }

    /// <summary>
    /// What the customer actually paid for this line: <see cref="LineTotal"/> less
    /// <see cref="DiscountShare"/>. Refunds come off this figure, and Module 8's profit
    /// reporting must use it rather than <see cref="LineTotal"/>.
    /// </summary>
    public decimal NetLineTotal { get; private set; }

    public Sale Sale { get; private set; } = null!;

    public Product Product { get; private set; } = null!;

    public Batch Batch { get; private set; } = null!;

    public IReadOnlyCollection<SalesReturn> Returns => _returns;

    /// <summary>Quantity that has come back against this line so far.</summary>
    public int ReturnedInBaseUnits => _returns.Sum(r => r.QuantityReturnedInBaseUnits);

    /// <summary>Quantity still returnable: what was sold, less what has already come back.</summary>
    public int ReturnableInBaseUnits => QuantityInBaseUnits - ReturnedInBaseUnits;

    /// <summary>Taka already refunded against this line.</summary>
    public decimal RefundedAmount => _returns.Sum(r => r.RefundAmount);

    /// <summary>
    /// Applies this line's share of the bill discount. Called once, by
    /// <see cref="Sale.ApplyDiscount"/>, which is what computes the shares as a set — a line
    /// cannot work out its own share without seeing the others.
    /// </summary>
    internal void SetDiscountShare(decimal share)
    {
        DiscountShare = share;
        NetLineTotal = LineTotal - share;
    }

    /// <summary>Attaches a return. Only <see cref="SalesReturn.Record"/> calls this.</summary>
    internal void Attach(SalesReturn salesReturn) => _returns.Add(salesReturn);
}
