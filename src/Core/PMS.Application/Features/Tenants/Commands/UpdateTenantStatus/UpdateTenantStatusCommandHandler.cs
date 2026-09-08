using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Tenants.Commands.UpdateTenantStatus;

public sealed class UpdateTenantStatusCommandHandler
    : IRequestHandler<UpdateTenantStatusCommand, Result<TenantDto>>
{
    private readonly IIdentityQueries _identity;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;

    public UpdateTenantStatusCommandHandler(
        IIdentityQueries identity, IUnitOfWork<IApplicationDbContext> unitOfWork)
    {
        _identity = identity;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TenantDto>> Handle(
        UpdateTenantStatusCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _identity.FindTenantByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<TenantDto>(Error.NotFound(nameof(Tenant), request.TenantId));
        }

        tenant.SetStatus(request.Status);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Takes effect for sessions already issued too: TenantResolutionMiddleware rechecks
        // status on every request, so suspending does not wait for tokens to expire.
        return new TenantDto(tenant.Id, tenant.Name, tenant.DomainName, tenant.Status,
            tenant.SubscriptionPlan, tenant.CreatedOnUtc);
    }
}
