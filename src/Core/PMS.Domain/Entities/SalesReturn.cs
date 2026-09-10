using Ardalis.GuardClauses;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;

namespace PMS.Domain.Entities;

/// <summary>
/// Goods coming back against one sale line, and the money that went back with them.
///
/// <para><b>Attached to a line, not to a sale.</b> A customer returns one item out of five,
/// and the line is what knows the batch the stock has to go back to and the discounted price
/// it was actually sold at. A return against a sale as a whole could not answer either.</para>
///
/// <para><b>Never edits the sale.</b> The original line keeps its quantity and its totals
/// forever; how much of it has come back is the sum of these rows. That is what lets an
/// invoice printed in March still be the invoice that was printed in March, with the returns
/// shown against it rather than folded into it.</para>
/// </summary>
public sealed class SalesReturn : BaseAuditableAggregateRoot<Guid>, ITenantEntity
{
    // EF materialises through this.
    private SalesReturn()
    {
    }

    private SalesReturn(
        Guid saleLineId,
        int quantityReturnedInBaseUnits,
        string reason,
        decimal refundAmount,
        Guid returnedByUserId)
    {
        Id = Guid.NewGuid();
        SaleLineId = saleLineId;
        QuantityReturnedInBaseUnits = quantityReturnedInBaseUnits;
        Reason = reason.Trim();
        RefundAmount = refundAmount;
        ReturnedByUserId = returnedByUserId;
    }

    /// <summary>
    /// Records a return and hangs it off the line, so the line's returned-quantity and
    /// refunded-total are correct in memory as well as in the database.
    ///
    /// <para>The refund is computed by the caller through <c>SaleMath.RefundFor</c> and passed
    /// in, because the rule needs the line's prior returns to make a completing return exact.
    /// The guard below is what stops a caller passing a figure that does not correspond to the
    /// quantity.</para>
    /// </summary>
    public static SalesReturn Record(
        SaleLine line,
        int quantityReturnedInBaseUnits,
        string reason,
        decimal refundAmount,
        Guid returnedByUserId)
    {
        ArgumentNullException.ThrowIfNull(line);
        Guard.Against.NullOrWhiteSpace(reason, nameof(reason));
        Guard.Against.Default(returnedByUserId, nameof(returnedByUserId));

        if (quantityReturnedInBaseUnits <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantityReturnedInBaseUnits), quantityReturnedInBaseUnits,
                "A return has to be for at least one unit.");
        }

        if (quantityReturnedInBaseUnits > line.ReturnableInBaseUnits)
        {
            // The last line of defence. The application layer checks first so the cashier gets
            // a message naming the figures; this exists so no future caller can create a
            // return for more than was sold, which would refund money against stock that never
            // left the pharmacy.
            throw new InvalidOperationException(
                $"{quantityReturnedInBaseUnits} cannot be returned against a line that has "
                + $"{line.ReturnableInBaseUnits} left to return "
                + $"(sold {line.QuantityInBaseUnits}, already returned {line.ReturnedInBaseUnits}).");
        }

        if (refundAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(refundAmount), refundAmount, "A refund cannot be negative.");
        }

        var salesReturn = new SalesReturn(
            line.Id, quantityReturnedInBaseUnits, reason, refundAmount, returnedByUserId);

        line.Attach(salesReturn);

        return salesReturn;
    }

    /// <summary>Stamped by the persistence interceptor on insert; never set by a handler.</summary>
    public Guid TenantId { get; set; }

    public Guid SaleLineId { get; private set; }

    public int QuantityReturnedInBaseUnits { get; private set; }

    /// <summary>
    /// Required. The screen offers quick-picks — changed mind, wrong item, defective — and
    /// still accepts free text, for the same reason a stock adjustment does: a fixed list gets
    /// answered "Other" for exactly the cases somebody will later need to understand.
    /// </summary>
    public string Reason { get; private set; } = null!;

    /// <summary>
    /// Money handed back, computed from the line's <c>NetLineTotal</c> so the discount is
    /// already absorbed. Stored rather than derived: the refund that empties a line pays the
    /// remainder rather than its own proportion, which a later recomputation from quantity
    /// alone could not reproduce.
    /// </summary>
    public decimal RefundAmount { get; private set; }

    public Guid ReturnedByUserId { get; private set; }

    public SaleLine SaleLine { get; private set; } = null!;
}
