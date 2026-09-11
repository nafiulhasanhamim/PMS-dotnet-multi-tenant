using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Settings.Queries.GetAntibioticMode;

public sealed class GetAntibioticModeQueryHandler
    : IRequestHandler<GetAntibioticModeQuery, Result<AntibioticModeDto>>
{
    private readonly ITenantSettings _settings;

    public GetAntibioticModeQueryHandler(ITenantSettings settings)
    {
        _settings = settings;
    }

    public async Task<Result<AntibioticModeDto>> Handle(
        GetAntibioticModeQuery request, CancellationToken cancellationToken)
        => Result.Success(new AntibioticModeDto(
            await _settings.GetAntibioticModeAsync(cancellationToken)));
}
