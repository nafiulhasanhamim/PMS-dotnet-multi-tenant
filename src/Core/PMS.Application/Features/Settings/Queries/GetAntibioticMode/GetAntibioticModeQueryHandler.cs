using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Settings;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Settings.Queries.GetAntibioticMode;

/// <summary>
/// The pharmacy's antibiotic prescription mode on its own.
///
/// <para>Kept alongside Module 10's whole-settings endpoint because this is the one setting an
/// Employee's till reads several times a day, and asking for every setting to answer one question
/// would make the billing screen's payload carry the pharmacy's discount caps and drug licence
/// number for no reason.</para>
/// </summary>
public sealed class GetAntibioticModeQueryHandler
    : IRequestHandler<GetAntibioticModeQuery, Result<AntibioticModeDto>>
{
    private readonly ISettingsService _settings;

    public GetAntibioticModeQueryHandler(ISettingsService settings)
    {
        _settings = settings;
    }

    public async Task<Result<AntibioticModeDto>> Handle(
        GetAntibioticModeQuery request, CancellationToken cancellationToken)
        => Result.Success(new AntibioticModeDto(
            await _settings.GetEnumAsync<AntibioticPrescriptionMode>(
                SettingKeys.AntibioticPrescriptionMode, cancellationToken)));
}
