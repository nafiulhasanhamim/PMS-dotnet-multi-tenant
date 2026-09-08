using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Tenants.Commands.CreateTenant;

public sealed class CreateTenantCommandHandler
    : IRequestHandler<CreateTenantCommand, Result<TenantDto>>
{
    private readonly IIdentityQueries _identity;
    private readonly IRepository<Tenant, IApplicationDbContext> _tenants;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;

    public CreateTenantCommandHandler(
        IIdentityQueries identity,
        IRepository<Tenant, IApplicationDbContext> tenants,
        IUnitOfWork<IApplicationDbContext> unitOfWork)
    {
        _identity = identity;
        _tenants = tenants;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TenantDto>> Handle(
        CreateTenantCommand request, CancellationToken cancellationToken)
    {
        // Checked against the *normalised* domain, so "Popular-Pharmacy" and
        // "https://popular-pharmacy/" both collide with an existing "popular-pharmacy".
        var clash = await _identity.FindTenantByDomainAsync(request.DomainName, cancellationToken);
        if (clash is not null)
        {
            return Result.Failure<TenantDto>(Error.Conflict(
                $"The domain '{request.DomainName}' is already in use."));
        }

        var tenant = new Tenant(request.Name, request.DomainName, request.SubscriptionPlan);
        await _tenants.AddAsync(tenant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TenantDto(tenant.Id, tenant.Name, tenant.DomainName, tenant.Status,
            tenant.SubscriptionPlan, tenant.CreatedOnUtc);
    }
}
