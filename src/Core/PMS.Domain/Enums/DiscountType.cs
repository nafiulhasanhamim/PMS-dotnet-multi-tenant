namespace PMS.Domain.Enums;

/// <summary>
/// How a bill-level discount was expressed by the cashier.
///
/// <para>Both end up as a taka figure in <c>Sale.DiscountAmount</c>, and that figure is what
/// the split and every report use. This enum survives only so the invoice can say
/// "Discount (5%)" rather than "Discount ৳62.00" — the customer asked for five percent off
/// and wants to see five percent off.</para>
/// </summary>
public enum DiscountType
{
    /// <summary>A percentage of the subtotal. <c>DiscountValue</c> is 5 for 5%.</summary>
    Percent = 0,

    /// <summary>A flat taka amount. <c>DiscountValue</c> is the amount itself.</summary>
    Flat = 1,
}
