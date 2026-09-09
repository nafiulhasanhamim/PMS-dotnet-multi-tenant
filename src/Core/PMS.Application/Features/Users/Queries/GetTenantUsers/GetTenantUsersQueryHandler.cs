using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Users.Queries.GetTenantUsers;

public sealed class GetTenantUsersQueryHandler
    : IRequestHandler<GetTenantUsersQuery, IReadOnlyList<TenantUserDto>>
{
    private readonly ITenantUserQueries _users;
    private readonly ILogger<GetTenantUsersQueryHandler> _logger;

    public GetTenantUsersQueryHandler(
        ITenantUserQueries users, ILogger<GetTenantUsersQueryHandler> logger)
    {
        _users = users;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TenantUserDto>> Handle(
        GetTenantUsersQuery request, CancellationToken cancellationToken)
    {
        // No tenant filter is written anywhere in this path — the membership query filter
        // does it. Another pharmacy's rows and the platform admins' null-tenant rows are
        // both absent by construction.
        var users = await _users.ListForCurrentTenantAsync(cancellationToken);

        // Says "own pharmacy" to separate it from the identically-named platform handler
        // under Features/Tenants. A count that ever looked too large for the pharmacy being
        // viewed would be the first visible sign that the filter above had stopped applying.
        _logger.LogDebug(
            "Own-pharmacy staff list returned {Count} memberships", users.Count);

        return users;
    }
}
