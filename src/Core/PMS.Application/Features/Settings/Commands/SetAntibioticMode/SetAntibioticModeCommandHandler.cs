using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Settings.Commands.SetAntibioticMode;

/// <summary>
/// Saves the pharmacy's antibiotic prescription mode.
///
/// <para>Reaches past the tenant query filter by design: <c>Tenants</c> is the tenant list rather
/// than tenant-owned data, so it is not an <c>ITenantEntity</c> and carries no automatic filter.
/// The id comes from the resolved tenant context, never from the request — an Admin at one
/// pharmacy must not be able to tighten or loosen another's rules.</para>
/// </summary>
public sealed class SetAntibioticModeCommandHandler
    : IRequestHandler<SetAntibioticModeCommand, Result<AntibioticModeDto>>
{
    private readonly IRepository<Tenant, IApplicationDbContext> _tenants;
    private readonly ICurrentTenantService _tenant;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<SetAntibioticModeCommandHandler> _logger;

    public SetAntibioticModeCommandHandler(
        IRepository<Tenant, IApplicationDbContext> tenants,
        ICurrentTenantService tenant,
        ICurrentUserService currentUser,
        ILogger<SetAntibioticModeCommandHandler> logger)
    {
        _tenants = tenants;
        _tenant = tenant;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<AntibioticModeDto>> Handle(
        SetAntibioticModeCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _tenants.GetByIdAsync(_tenant.TenantId, cancellationToken);

        if (tenant is null)
        {
            _logger.LogError(
                "Antibiotic mode not changed: tenant {TenantId} was resolved for the request "
                + "but does not exist",
                _tenant.TenantId);

            return Result.Failure<AntibioticModeDto>(
                Error.NotFound(nameof(Tenant), _tenant.TenantId));
        }

        var previous = tenant.AntibioticPrescriptionMode;

        if (previous == request.Mode)
        {
            // Not an error, and not worth a write. Somebody re-saving the mode they are already
            // on is a person confirming something, and telling them it failed would be absurd.
            return Result.Success(new AntibioticModeDto(previous));
        }

        tenant.SetAntibioticPrescriptionMode(request.Mode);
        await _tenants.UpdateAsync(tenant, cancellationToken);

        // Information, and it names both ends. This setting has legal consequences in both
        // directions: tightening it stops Employees dispensing mid-shift, and loosening it stops
        // prescriptions being recorded at all. "Who changed it and when" is the first question
        // asked after either surprise.
        _logger.LogInformation(
            "Antibiotic prescription mode changed for tenant {TenantId} ({TenantName}): "
            + "{PreviousMode} -> {NewMode} by {UserId}. Takes effect on the next sale; past "
            + "sales are not reinterpreted.",
            tenant.Id, tenant.Name, previous, request.Mode, _currentUser.UserGuid);

        return Result.Success(new AntibioticModeDto(request.Mode));
    }
}
