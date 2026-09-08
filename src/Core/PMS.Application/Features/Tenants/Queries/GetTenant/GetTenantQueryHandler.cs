using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Tenants.Queries.GetTenant;

public sealed class GetTenantQueryHandler : IRequestHandler<GetTenantQuery, Result<TenantDto>>
{
    private readonly IIdentityQueries _identity;

    public GetTenantQueryHandler(IIdentityQueries identity)
    {
        _identity = identity;
    }

    public async Task<Result<TenantDto>> Handle(
        GetTenantQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _identity.FindTenantByIdAsync(request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<TenantDto>(Error.NotFound("Tenant", request.TenantId));
        }

        return new TenantDto(tenant.Id, tenant.Name, tenant.DomainName, tenant.Status,
            tenant.SubscriptionPlan, tenant.CreatedOnUtc);
    }
}
