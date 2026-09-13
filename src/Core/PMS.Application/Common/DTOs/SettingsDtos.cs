using PMS.Domain.Enums;

namespace PMS.Application.Common.DTOs;

/// <summary>
/// Every setting this pharmacy has, typed.
///
/// <para><b>A typed record rather than the raw key-value list.</b> The storage is key-value
/// because that makes adding a setting a seed row instead of a migration — but a client that had
/// to know <c>expiry_alert_window_days</c> holds an integer would be a second place the types
/// live, and the first place to drift. The cast happens once, in <c>ISettingsService</c>, and
/// this is its shape.</para>
///
/// <para>Flat rather than grouped. The settings screen groups these into four sections visually,
/// but a nested contract would mean a partial update had to name a section to change one field
/// in it.</para>
/// </summary>
public sealed record SettingsDto(
    string PharmacyName,
    string PharmacyAddress,
    string PharmacyPhone,
    string PharmacyLicenseNumber,
    int ExpiryAlertWindowDays,
    int DeadStockThresholdDays,
    int DefaultReorderLevel,
    int DiscountCapEmployeePercent,
    int DiscountCapPharmacistPercent,
    AntibioticPrescriptionMode AntibioticPrescriptionMode);

/// <summary>
/// What the pharmacy calls itself, for documents that have to say whose record they are.
///
/// <para>A subset of <see cref="SettingsDto"/>, because the invoice and the salary slip need
/// exactly these four and have no business carrying a discount cap to the printer.</para>
/// </summary>
public sealed record PharmacyProfileDto(
    string Name,
    string Address,
    string Phone,
    string LicenseNumber)
{
    public bool HasAddress => !string.IsNullOrWhiteSpace(Address);

    public bool HasPhone => !string.IsNullOrWhiteSpace(Phone);

    public bool HasLicense => !string.IsNullOrWhiteSpace(LicenseNumber);
}

/// <summary>What a save actually changed, so the screen can say something true.</summary>
public sealed record SettingsSavedDto(SettingsDto Settings, IReadOnlyList<string> ChangedKeys)
{
    public bool AnythingChanged => ChangedKeys.Count > 0;
}
