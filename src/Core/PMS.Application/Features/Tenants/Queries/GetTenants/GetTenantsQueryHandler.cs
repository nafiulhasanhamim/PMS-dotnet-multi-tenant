using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Tenants.Queries.GetTenants;

public sealed class GetTenantsQueryHandler
    : IRequestHandler<GetTenantsQuery, IReadOnlyList<TenantDto>>
{
    private readonly IPlatformQueries _platform;
    private readonly ILogger<GetTenantsQueryHandler> _logger;

    public GetTenantsQueryHandler(
        IPlatformQueries platform, ILogger<GetTenantsQueryHandler> logger)
    {
        _platform = platform;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TenantDto>> Handle(
        GetTenantsQuery request, CancellationToken cancellationToken)
    {
        var tenants = await _platform.ListTenantsAsync(cancellationToken);

        // Debug, like every other list in this codebase: it cannot fail, so the only thing
        // to record is that it ran, and the pipeline behaviour already does that with a
        // duration attached. The count is here for the day this query has to be paged.
        _logger.LogDebug("Platform tenant list returned {Count} tenants", tenants.Count);

        return tenants;
    }
}
