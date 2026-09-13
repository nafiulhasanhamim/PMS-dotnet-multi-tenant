using PMS.Application.Common.Settings;
using PMS.Application.Interfaces;

namespace PMS.Application.Common.Billing;

/// <summary>
/// What this pharmacy lets each non-Admin role discount, as a percentage.
///
/// <para>A record rather than two loose decimals so a caller cannot transpose them. "Employee 10,
/// Pharmacist 5" is a legal-looking argument list and a serious bug: it would let the least
/// trusted role discount the most.</para>
///
/// <para>There is no Admin member. An Admin is unlimited and that is not configurable — see
/// <see cref="BillingPolicy.MaxDiscountPercentFor"/>.</para>
/// </summary>
public readonly record struct DiscountCaps(decimal EmployeePercent, decimal PharmacistPercent)
{
    /// <summary>
    /// Reads both caps from settings in one go.
    ///
    /// <para>An extension rather than a constructor overload taking the service, so
    /// <see cref="DiscountCaps"/> itself stays a plain value that a unit test can build without a
    /// database — which is what keeps <see cref="BillingPolicy"/> testable.</para>
    /// </summary>
    public static async Task<DiscountCaps> FromSettingsAsync(
        ISettingsService settings, CancellationToken cancellationToken = default) =>
        new(
            await settings.GetIntAsync(
                SettingKeys.DiscountCapEmployeePercent, cancellationToken),
            await settings.GetIntAsync(
                SettingKeys.DiscountCapPharmacistPercent, cancellationToken));
}
