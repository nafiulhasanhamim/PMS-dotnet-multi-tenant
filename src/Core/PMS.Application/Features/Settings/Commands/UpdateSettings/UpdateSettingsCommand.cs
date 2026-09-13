using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Settings.Commands.UpdateSettings;

/// <summary>
/// Changes some or all of this pharmacy's settings. <b>Admin only.</b>
///
/// <para><b>Every field is nullable, and null means "leave it alone".</b> That is what lets the
/// settings page post the whole form while a future caller changes one value, without either
/// having to know about the other. It also keeps "clear the pharmacy name" expressible and
/// distinguishable: an empty string is a value being set, and the validator refuses it.</para>
///
/// <para><b>Nothing is applied unless everything validates.</b> A request carrying a good
/// pharmacy name and a discount cap of 150 changes neither — a half-applied settings save would
/// leave a pharmacy in a state nobody asked for and no screen would show as incomplete.</para>
/// </summary>
public sealed record UpdateSettingsCommand(
    string? PharmacyName,
    string? PharmacyAddress,
    string? PharmacyPhone,
    string? PharmacyLicenseNumber,
    int? ExpiryAlertWindowDays,
    int? DeadStockThresholdDays,
    int? DefaultReorderLevel,
    int? DiscountCapEmployeePercent,
    int? DiscountCapPharmacistPercent,
    AntibioticPrescriptionMode? AntibioticPrescriptionMode)
    : IRequest<Result<SettingsSavedDto>>, ITenantScopedRequest;
