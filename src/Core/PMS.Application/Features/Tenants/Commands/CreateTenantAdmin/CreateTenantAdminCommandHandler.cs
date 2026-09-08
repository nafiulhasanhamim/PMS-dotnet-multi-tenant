using PMS.Application.Common.DTOs;
using PMS.Application.Common.Provisioning;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Tenants.Commands.CreateTenantAdmin;

public sealed class CreateTenantAdminCommandHandler
    : IRequestHandler<CreateTenantAdminCommand, Result<ProvisionedUserDto>>
{
    private readonly IIdentityQueries _identity;
    private readonly UserProvisioner _provisioner;

    public CreateTenantAdminCommandHandler(IIdentityQueries identity, UserProvisioner provisioner)
    {
        _identity = identity;
        _provisioner = provisioner;
    }

    public async Task<Result<ProvisionedUserDto>> Handle(
        CreateTenantAdminCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _identity.FindTenantByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<ProvisionedUserDto>(
                Error.NotFound(nameof(Tenant), request.TenantId));
        }

        return await _provisioner.ProvisionAsync(
            tenant.Id, request.Email, request.FullName, request.Password,
            UserRole.Admin, cancellationToken);
    }
}
