using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Settings;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Settings.Commands.SetAntibioticMode;

/// <summary>
/// Saves the pharmacy's antibiotic prescription mode.
///
/// <para><b>Module 10 moved where this is stored.</b> It was a column on the <c>Tenants</c> row,
/// which migration 012 chose only because no settings table existed yet — and which said, in as
/// many words, that a Settings module should move it. It is now one row in <c>AppSettings</c>
/// like every other preference, written through <c>ISettingsService</c>: the same path the
/// settings page uses. One store, one writer, nothing to drift.</para>
///
/// <para>This endpoint survives the move because Module 7's own settings screen calls it, and
/// because "change one setting" is a smaller thing to ask for than "save the whole page". Both
/// end up in the same table through the same service.</para>
/// </summary>
public sealed class SetAntibioticModeCommandHandler
    : IRequestHandler<SetAntibioticModeCommand, Result<AntibioticModeDto>>
{
    private readonly ISettingsService _settings;
    private readonly ICurrentTenantService _tenant;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<SetAntibioticModeCommandHandler> _logger;

    public SetAntibioticModeCommandHandler(
        ISettingsService settings,
        ICurrentTenantService tenant,
        ICurrentUserService currentUser,
        ILogger<SetAntibioticModeCommandHandler> logger)
    {
        _settings = settings;
        _tenant = tenant;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<AntibioticModeDto>> Handle(
        SetAntibioticModeCommand request, CancellationToken cancellationToken)
    {
        var previous = await _settings.GetEnumAsync<AntibioticPrescriptionMode>(
            SettingKeys.AntibioticPrescriptionMode, cancellationToken);

        if (previous == request.Mode)
        {
            // Not an error, and not worth a write. Somebody re-saving the mode they are already
            // on is a person confirming something, and telling them it failed would be absurd.
            return Result.Success(new AntibioticModeDto(previous));
        }

        // Stored as the enum's NAME rather than its number: a settings table read by a person
        // during an incident should say "Required", not "2".
        await _settings.SaveAsync(
            new Dictionary<string, string>
            {
                [SettingKeys.AntibioticPrescriptionMode] = request.Mode.ToString(),
            },
            _currentUser.UserGuid,
            cancellationToken);

        // Information, and it names both ends. This setting has legal consequences in both
        // directions: tightening it stops Employees dispensing mid-shift, and loosening it stops
        // prescriptions being recorded at all. "Who changed it and when" is the first question
        // asked after either surprise.
        _logger.LogInformation(
            "Antibiotic prescription mode changed for tenant {TenantId}: "
            + "{PreviousMode} -> {NewMode} by {UserId}. Takes effect on the next sale; past "
            + "sales are not reinterpreted.",
            _tenant.TenantId, previous, request.Mode, _currentUser.UserGuid);

        return Result.Success(new AntibioticModeDto(request.Mode));
    }
}
