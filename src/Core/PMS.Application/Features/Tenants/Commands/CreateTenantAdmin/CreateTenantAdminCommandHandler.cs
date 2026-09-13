using PMS.Application.Common.DTOs;
using PMS.Application.Common.Provisioning;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Tenants.Commands.CreateTenantAdmin;

public sealed class CreateTenantAdminCommandHandler
    : IRequestHandler<CreateTenantAdminCommand, Result<ProvisionedUserDto>>
{
    private readonly IIdentityQueries _identity;
    private readonly UserProvisioner _provisioner;
    private readonly ILogger<CreateTenantAdminCommandHandler> _logger;

    public CreateTenantAdminCommandHandler(
        IIdentityQueries identity,
        UserProvisioner provisioner,
        ILogger<CreateTenantAdminCommandHandler> logger)
    {
        _identity = identity;
        _provisioner = provisioner;
        _logger = logger;
    }

    public async Task<Result<ProvisionedUserDto>> Handle(
        CreateTenantAdminCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _identity.FindTenantByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            _logger.LogWarning(
                "Tenant admin not created for {Email}: no tenant {TenantId} exists",
                request.Email, request.TenantId);

            return Result.Failure<ProvisionedUserDto>(
                Error.NotFound(nameof(Tenant), request.TenantId));
        }

        // Every other outcome — created, linked, refused — is logged inside UserProvisioner,
        // once, for both this endpoint and the tenant-side one. Repeating it here would
        // double every line in the file for the platform path only.
        //
        // What that shared line cannot say is which endpoint was used, and here it matters:
        // this one hands out a pharmacy's *first* Admin, and it is a platform operator
        // reaching into a tenant to do it.
        _logger.LogInformation(
            "Platform is provisioning an Admin for {TenantName} ({TenantId}): {Email}",
            tenant.Name, tenant.Id, request.Email);

        return await _provisioner.ProvisionAsync(
            tenant.Id, request.Email, request.FullName, request.Password,
            UserRole.Admin, cancellationToken);
    }
}
