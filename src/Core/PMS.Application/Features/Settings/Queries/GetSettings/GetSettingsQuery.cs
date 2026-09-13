using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Settings;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Settings.Queries.GetSettings;

/// <summary>
/// Every setting for the current pharmacy.
///
/// <para><b>Readable by any tenant user, not just an Admin.</b> The billing screen needs the
/// discount caps and the antibiotic mode on every load, and an invoice needs the pharmacy's name
/// and phone — so a cashier reads these several times a day without ever being able to change
/// one. Writing is Admin-only; see <c>UpdateSettingsCommand</c>.</para>
/// </summary>
public sealed record GetSettingsQuery : IRequest<Result<SettingsDto>>, ITenantScopedRequest;

public sealed class GetSettingsQueryHandler
    : IRequestHandler<GetSettingsQuery, Result<SettingsDto>>
{
    private readonly ISettingsService _settings;

    public GetSettingsQueryHandler(ISettingsService settings) => _settings = settings;

    public async Task<Result<SettingsDto>> Handle(
        GetSettingsQuery request, CancellationToken cancellationToken)
    {
        // One read behind all of these - the service caches for the request, so this is a single
        // query however many keys the DTO has.
        var dto = new SettingsDto(
            await _settings.GetStringAsync(SettingKeys.PharmacyName, cancellationToken),
            await _settings.GetStringAsync(SettingKeys.PharmacyAddress, cancellationToken),
            await _settings.GetStringAsync(SettingKeys.PharmacyPhone, cancellationToken),
            await _settings.GetStringAsync(SettingKeys.PharmacyLicenseNumber, cancellationToken),
            await _settings.GetIntAsync(SettingKeys.ExpiryAlertWindowDays, cancellationToken),
            await _settings.GetIntAsync(SettingKeys.DeadStockThresholdDays, cancellationToken),
            await _settings.GetIntAsync(SettingKeys.DefaultReorderLevel, cancellationToken),
            await _settings.GetIntAsync(
                SettingKeys.DiscountCapEmployeePercent, cancellationToken),
            await _settings.GetIntAsync(
                SettingKeys.DiscountCapPharmacistPercent, cancellationToken),
            await _settings.GetEnumAsync<AntibioticPrescriptionMode>(
                SettingKeys.AntibioticPrescriptionMode, cancellationToken));

        return Result.Success(dto);
    }
}
