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
        //
        // Deliberately no ILogger here. UserProvisioner logs every outcome of this — granted,
        // refused, and why — and its line already carries the tenant id, the email and the
        // role, which is everything this handler knows. The platform-side CreateTenantAdmin
        // does add a line of its own, but only because it can say something the shared one
        // cannot: that an operator outside the pharmacy is the one reaching in.
        _provisioner.ProvisionAsync(
            _tenant.TenantId, request.Email, request.FullName, request.Password,
            request.Role, cancellationToken);
}
