using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Tenants.Queries.GetTenantUsers;

public sealed class GetTenantUsersQueryHandler
    : IRequestHandler<GetTenantUsersQuery, IReadOnlyList<TenantUserDto>>
{
    private readonly IIdentityQueries _identity;
    private readonly ILogger<GetTenantUsersQueryHandler> _logger;

    public GetTenantUsersQueryHandler(
        IIdentityQueries identity, ILogger<GetTenantUsersQueryHandler> logger)
    {
        _identity = identity;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TenantUserDto>> Handle(
        GetTenantUsersQuery request, CancellationToken cancellationToken)
    {
        var memberships = await _identity.ListMembershipsForTenantAsync(
            request.TenantId, cancellationToken);

        // The wording says "platform view" on purpose. There is a second handler with this
        // exact class name under Features/Users that lists the *caller's own* pharmacy, and
        // the logger category alone (which prints the namespace) is a thin thing to tell
        // them apart by when reading a file at speed. This is the one that reaches into a
        // named tenant from outside it.
        _logger.LogDebug(
            "Platform view of tenant {TenantId} staff returned {Count} memberships",
            request.TenantId, memberships.Count);

        return memberships
            .Select(m => new TenantUserDto(m.Id, m.UserId, m.User.Email, m.User.FullName,
                m.Role, m.IsActive, m.JoinedAt))
            .ToList();
    }
}
