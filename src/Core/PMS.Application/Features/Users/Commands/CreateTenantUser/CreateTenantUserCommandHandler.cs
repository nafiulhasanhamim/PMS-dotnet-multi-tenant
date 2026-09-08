using PMS.Application.Common.DTOs;
using PMS.Application.Common.Provisioning;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Users.Commands.CreateTenantUser;

public sealed class CreateTenantUserCommandHandler
    : IRequestHandler<CreateTenantUserCommand, Result<ProvisionedUserDto>>
{
    private readonly ICurrentTenantService _tenant;
    private readonly UserProvisioner _provisioner;

    public CreateTenantUserCommandHandler(
        ICurrentTenantService tenant, UserProvisioner provisioner)
    {
        _tenant = tenant;
        _provisioner = provisioner;
    }

    public Task<Result<ProvisionedUserDto>> Handle(
        CreateTenantUserCommand request, CancellationToken cancellationToken) =>
        // The tenant is taken from the caller's token. TenantValidationBehavior has already
        // refused the request if there was none.
        _provisioner.ProvisionAsync(
            _tenant.TenantId, request.Email, request.FullName, request.Password,
            request.Role, cancellationToken);
}
