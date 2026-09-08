using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using MediatR;

namespace PMS.Application.Features.Tenants.Queries.GetTenants;

public sealed class GetTenantsQueryHandler
    : IRequestHandler<GetTenantsQuery, IReadOnlyList<TenantDto>>
{
    private readonly IPlatformQueries _platform;

    public GetTenantsQueryHandler(IPlatformQueries platform)
    {
        _platform = platform;
    }

    public Task<IReadOnlyList<TenantDto>> Handle(
        GetTenantsQuery request, CancellationToken cancellationToken) =>
        _platform.ListTenantsAsync(cancellationToken);
}
