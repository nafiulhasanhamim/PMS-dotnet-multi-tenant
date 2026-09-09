using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Tenants.Queries.GetTenant;

public sealed class GetTenantQueryHandler : IRequestHandler<GetTenantQuery, Result<TenantDto>>
{
    private readonly IIdentityQueries _identity;
    private readonly ILogger<GetTenantQueryHandler> _logger;

    public GetTenantQueryHandler(
        IIdentityQueries identity, ILogger<GetTenantQueryHandler> logger)
    {
        _identity = identity;
        _logger = logger;
    }

    public async Task<Result<TenantDto>> Handle(
        GetTenantQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _identity.FindTenantByIdAsync(request.TenantId, cancellationToken);

        if (tenant is null)
        {
            _logger.LogWarning("Tenant {TenantId} not found", request.TenantId);

            return Result.Failure<TenantDto>(Error.NotFound("Tenant", request.TenantId));
        }

        return new TenantDto(tenant.Id, tenant.Name, tenant.DomainName, tenant.Status,
            tenant.SubscriptionPlan, tenant.CreatedOnUtc);
    }
}
