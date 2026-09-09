using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Tenants.Commands.CreateTenant;

public sealed class CreateTenantCommandHandler
    : IRequestHandler<CreateTenantCommand, Result<TenantDto>>
{
    private readonly IIdentityQueries _identity;
    private readonly IRepository<Tenant, IApplicationDbContext> _tenants;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ILogger<CreateTenantCommandHandler> _logger;

    public CreateTenantCommandHandler(
        IIdentityQueries identity,
        IRepository<Tenant, IApplicationDbContext> tenants,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ILogger<CreateTenantCommandHandler> logger)
    {
        _identity = identity;
        _tenants = tenants;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<TenantDto>> Handle(
        CreateTenantCommand request, CancellationToken cancellationToken)
    {
        // Checked against the *normalised* domain, so "Popular-Pharmacy" and
        // "https://popular-pharmacy/" both collide with an existing "popular-pharmacy".
        var clash = await _identity.FindTenantByDomainAsync(request.DomainName, cancellationToken);
        if (clash is not null)
        {
            // Names the pharmacy already holding the domain, because normalisation makes the
            // clash non-obvious: someone typing "https://Popular-Pharmacy/" is told a domain
            // they never typed is taken. This line shows what it normalised to and who has it.
            _logger.LogWarning(
                "Tenant not created: domain {DomainName} normalises onto {ExistingTenantName} "
                + "({ExistingTenantId})",
                request.DomainName, clash.Name, clash.Id);

            return Result.Failure<TenantDto>(Error.Conflict(
                $"The domain '{request.DomainName}' is already in use."));
        }

        var tenant = new Tenant(request.Name, request.DomainName, request.SubscriptionPlan);
        await _tenants.AddAsync(tenant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // A new pharmacy on the platform: rare, deliberate, and the root of every tenant id
        // that will appear in this file from now on. The stored domain is logged rather than
        // the requested one, since that is what a login will have to match.
        _logger.LogInformation(
            "Tenant created {TenantId} {TenantName} at domain {DomainName} on the "
            + "{SubscriptionPlan} plan",
            tenant.Id, tenant.Name, tenant.DomainName, tenant.SubscriptionPlan);

        return new TenantDto(tenant.Id, tenant.Name, tenant.DomainName, tenant.Status,
            tenant.SubscriptionPlan, tenant.CreatedOnUtc);
    }
}
