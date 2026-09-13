using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Settings;
using PMS.Application.Features.Settings.Queries.GetSettings;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Settings.Commands.UpdateSettings;

/// <summary>
/// <para>By the time this runs the validator has already refused anything out of range, so the
/// write below is unconditional — which is what makes "nothing is applied unless everything
/// validates" true rather than aspirational. The pipeline refuses the whole request; this handler
/// never sees a partially valid one.</para>
///
/// <para>The settings are re-read afterwards rather than assembled from the request, so the
/// response says what is <em>stored</em>. A caller that sent nothing gets the current values
/// back, which is what the settings page then renders.</para>
/// </summary>
public sealed class UpdateSettingsCommandHandler
    : IRequestHandler<UpdateSettingsCommand, Result<SettingsSavedDto>>
{
    private readonly ISettingsService _settings;
    private readonly ISender _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<UpdateSettingsCommandHandler> _logger;

    public UpdateSettingsCommandHandler(
        ISettingsService settings,
        ISender mediator,
        ICurrentUserService currentUser,
        ILogger<UpdateSettingsCommandHandler> logger)
    {
        _settings = settings;
        _mediator = mediator;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<SettingsSavedDto>> Handle(
        UpdateSettingsCommand request, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        // Null means "leave it alone", so only supplied fields reach the dictionary. Text values
        // are trimmed; a pharmacy name with a trailing space would print on every invoice.
        Add(values, SettingKeys.PharmacyName, request.PharmacyName?.Trim());
        Add(values, SettingKeys.PharmacyAddress, request.PharmacyAddress?.Trim());
        Add(values, SettingKeys.PharmacyPhone, request.PharmacyPhone?.Trim());
        Add(values, SettingKeys.PharmacyLicenseNumber, request.PharmacyLicenseNumber?.Trim());

        Add(values, SettingKeys.ExpiryAlertWindowDays,
            request.ExpiryAlertWindowDays?.ToString());
        Add(values, SettingKeys.DeadStockThresholdDays,
            request.DeadStockThresholdDays?.ToString());
        Add(values, SettingKeys.DefaultReorderLevel,
            request.DefaultReorderLevel?.ToString());
        Add(values, SettingKeys.DiscountCapEmployeePercent,
            request.DiscountCapEmployeePercent?.ToString());
        Add(values, SettingKeys.DiscountCapPharmacistPercent,
            request.DiscountCapPharmacistPercent?.ToString());

        // The enum's NAME, not its number: a settings table read by a person during an incident
        // should say "Required", not "2".
        Add(values, SettingKeys.AntibioticPrescriptionMode,
            request.AntibioticPrescriptionMode?.ToString());

        var changed = await _settings.SaveAsync(
            values, _currentUser.UserGuid, cancellationToken);

        if (changed.Count > 0)
        {
            // Named individually, and at Information. These change what staff may do and what
            // appears on documents the pharmacy hands out; "who changed the discount cap and
            // when" is the first question asked after a surprise at the till.
            _logger.LogInformation(
                "Settings changed by {UserId}: {ChangedKeys}",
                _currentUser.UserGuid, string.Join(", ", changed));
        }

        // Re-read through the same query the settings screen uses, so the response is what is
        // stored rather than what was asked for.
        var current = await _mediator.Send(new GetSettingsQuery(), cancellationToken);

        return current.IsSuccess
            ? Result.Success(new SettingsSavedDto(current.Value!, changed.ToList()))
            : Result.Failure<SettingsSavedDto>(current.Error);
    }

    private static void Add(Dictionary<string, string> values, string key, string? value)
    {
        if (value is not null)
        {
            values[key] = value;
        }
    }
}
