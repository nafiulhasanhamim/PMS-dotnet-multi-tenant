using PMS.Domain.Billing;
using PMS.Domain.Enums;

namespace PMS.Application.Common.Billing;

/// <summary>
/// What a role may discount, as a rule rather than as a number.
///
/// <para><b>Module 10 took the two numbers out of this class and left the rule behind.</b> The
/// caps are per-pharmacy now and arrive as a <see cref="DiscountCaps"/> argument read from
/// <c>ISettingsService</c>; the fallback defaults are declared once in <c>SettingKeys</c>.
/// Gathering the constants here first is what made that a small change — every caller already
/// came through these methods rather than comparing against a literal of its own.</para>
///
/// <para><b>Still pure functions, and that is the point.</b> The server-side check at sale
/// completion, the refusal message the cashier reads and the helper text on the billing screen
/// are the same rule evaluated three times. Passing the caps in rather than reading them here
/// keeps that true and keeps the rule testable without a database.</para>
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
    /// <para><b>An Admin is unlimited, and that is not configurable.</b> An owner writing off a
    /// whole bill for a regular customer is a legitimate thing an owner does, and a cap they can
    /// lift themselves is not a control — so there is no <c>discount_cap_admin_percent</c>
    /// setting and there should not be one.</para>
    /// </summary>
    public static decimal? MaxDiscountPercentFor(UserRole role, DiscountCaps caps) => role switch
    {
        UserRole.Employee => caps.EmployeePercent,
        UserRole.Pharmacist => caps.PharmacistPercent,
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
    public static decimal? MaxDiscountAmountFor(
        UserRole role, decimal subtotal, DiscountCaps caps)
    {
        var percent = MaxDiscountPercentFor(role, caps);

        return percent is null ? null : SaleMath.Round(subtotal * percent.Value / 100m);
    }

    /// <summary>
    /// Whether this role may take <paramref name="discountAmount"/> off
    /// <paramref name="subtotal"/>.
    /// </summary>
    public static bool IsDiscountAllowed(
        UserRole role, decimal subtotal, decimal discountAmount, DiscountCaps caps)
    {
        var cap = MaxDiscountAmountFor(role, subtotal, caps);

        return cap is null || discountAmount <= cap.Value;
    }

    /// <summary>
    /// The refusal message, which states the caller's own maximum in both forms.
    ///
    /// <para>Saying "too large" alone leaves a cashier guessing, and guessing at a counter with
    /// a customer waiting means calling someone over. The taka figure is the one they can act
    /// on; the percentage is the one that explains why.</para>
    /// </summary>
    public static string DiscountRefusalMessage(
        UserRole role, decimal subtotal, DiscountCaps caps)
    {
        var percent = MaxDiscountPercentFor(role, caps);
        var cap = MaxDiscountAmountFor(role, subtotal, caps);

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
    public static string DescribeCap(UserRole role, DiscountCaps caps)
    {
        var percent = MaxDiscountPercentFor(role, caps);

        return percent is null
            ? "No discount limit"
            : $"Max discount: {percent.Value:0.##}%";
    }
}
