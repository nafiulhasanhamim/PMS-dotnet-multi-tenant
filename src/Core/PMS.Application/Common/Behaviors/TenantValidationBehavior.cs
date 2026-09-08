using PMS.Application.Interfaces;
using PMS.SharedKernel.Interfaces;
using MediatR;

namespace PMS.Application.Common.Behaviors;

/// <summary>
/// Refuses a tenant-scoped request when no tenant is resolved.
///
/// Without this, such a request would still run: the query filter would compare against
/// Guid.Empty and quietly return nothing, so the caller would see an empty pharmacy rather
/// than an error, and the cause would be invisible. Failing here names the problem instead.
/// </summary>
public sealed class TenantValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentTenantService _tenant;

    public TenantValidationBehavior(ICurrentTenantService tenant)
    {
        _tenant = tenant;
    }

    public Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is ITenantScopedRequest && !_tenant.HasTenant)
        {
            throw new UnauthorizedAccessException(
                $"{typeof(TRequest).Name} requires a pharmacy context, but the request carries " +
                "no tenant. Sign in at a pharmacy's domain first.");
        }

        return next();
    }
}
