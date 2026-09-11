using PMS.Domain.Billing;
using PMS.Domain.Enums;

namespace PMS.Application.Common.Billing;

/// <summary>
/// The numbers a pharmacy owner would want to change, in one place.
///
/// <para><b>Hard-coded on purpose, and hard-coded in exactly one file.</b> A Settings module
/// will eventually make these per-pharmacy, and the work of that module is then to replace this
/// class rather than to hunt for the constants scattered through six handlers and four Razor
/// pages. Every caller — the completion handler, the validator, the billing screen's helper
/// text — reads them from here.</para>
///
/// <para><b>Why a cap at all.</b> A discount is the one control at the counter that moves money
/// out of the business with no stock leaving the shelf, so it is where a counter clerk under
/// pressure — or in collusion — does the most damage. Caps are the reason the bill-level
/// discount can be trusted to staff who need it.</para>
/// </summary>
public static class BillingPolicy
{
    /// <summary>Sales list page size. Matches the other grids in the product.</summary>
    public const int SalesPageSize = 25;

    /// <summary>
    /// How many results the sellable-product type-ahead returns. Small deliberately: a cashier
    /// types two or three letters and picks from what they can see without scrolling, and a
    /// longer list is slower to read, not more useful.
    /// </summary>
    public const int SellableSearchLimit = 20;

    /// <summary>
    /// The most a role may discount, as a percentage of the subtotal. Null means no limit.
    ///
    /// <para>An Admin is unlimited because an owner writing off a whole bill for a regular
    /// customer is a legitimate thing an owner does, and a cap they can lift themselves is not
    /// a control.</para>
    /// </summary>
    public static decimal? MaxDiscountPercentFor(UserRole role) => role switch
    {
        UserRole.Employee => 5m,
        UserRole.Pharmacist => 10m,
        UserRole.Admin => null,

        // A platform operator has no membership at a pharmacy and so cannot reach a till at
        // all; the tenant policies refuse the request long before this. Zero rather than null
        // so that a future caller who gets here by accident is refused, not waved through.
        _ => 0m,
    };

    /// <summary>
    /// The cap expressed in taka for a given subtotal, or null when the role is unlimited.
    ///
    /// <para><b>This is how a flat discount is capped.</b> "Employee, maximum 5%" has to mean
    /// something for a cashier who types ৳100 off rather than 5% — otherwise the cap is a
    /// formatting preference rather than a control, and the way round it is to use the other
    /// button. On a ৳500 bill an Employee may take off at most ৳25, however they express
    /// it.</para>
    /// </summary>
    public static decimal? MaxDiscountAmountFor(UserRole role, decimal subtotal)
    {
        var percent = MaxDiscountPercentFor(role);

        return percent is null ? null : SaleMath.Round(subtotal * percent.Value / 100m);
    }

    /// <summary>
    /// Whether this role may take <paramref name="discountAmount"/> off
    /// <paramref name="subtotal"/>.
    /// </summary>
    public static bool IsDiscountAllowed(UserRole role, decimal subtotal, decimal discountAmount)
    {
        var cap = MaxDiscountAmountFor(role, subtotal);

        return cap is null || discountAmount <= cap.Value;
    }

    /// <summary>
    /// The refusal message, which states the caller's own maximum in both forms.
    ///
    /// <para>Saying "too large" alone leaves a cashier guessing, and guessing at a counter with
    /// a customer waiting means calling someone over. The taka figure is the one they can act
    /// on; the percentage is the one that explains why.</para>
    /// </summary>
    public static string DiscountRefusalMessage(UserRole role, decimal subtotal)
    {
        var percent = MaxDiscountPercentFor(role);
        var cap = MaxDiscountAmountFor(role, subtotal);

        if (percent is null || cap is null)
        {
            return "That discount is larger than the bill.";
        }

        return $"Your maximum discount is {percent.Value:0.##}%, which is "
            + $"{cap.Value:0.00} on a subtotal of {subtotal:0.00}.";
    }

    /// <summary>
    /// Whether this role may dispense an antibiotic at a pharmacy on this mode.
    ///
    /// <para>Here rather than inline in two handlers, because it is read by the completion
    /// handler, by the sellable-product search and by the limits endpoint — and three copies of
    /// a rule with legal consequences is three chances to get it wrong differently.</para>
    ///
    /// <para><b>Only Required restricts it.</b> Under Off and Optional an Employee sells an
    /// antibiotic like any other product. See <see cref="AntibioticPrescriptionMode"/> for why
    /// the default is the loose end of that.</para>
    /// </summary>
    public static bool MaySellAntibiotics(UserRole role, AntibioticPrescriptionMode mode) =>
        mode != AntibioticPrescriptionMode.Required
        || role is UserRole.Admin or UserRole.Pharmacist;

    /// <summary>The cap as helper text for the billing screen — "Max discount: 10%".</summary>
    public static string DescribeCap(UserRole role)
    {
        var percent = MaxDiscountPercentFor(role);

        return percent is null
            ? "No discount limit"
            : $"Max discount: {percent.Value:0.##}%";
    }
}
