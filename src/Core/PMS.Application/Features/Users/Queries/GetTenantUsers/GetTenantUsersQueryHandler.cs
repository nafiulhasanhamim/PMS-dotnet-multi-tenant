using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using MediatR;

namespace PMS.Application.Features.Users.Queries.GetTenantUsers;

public sealed class GetTenantUsersQueryHandler
    : IRequestHandler<GetTenantUsersQuery, IReadOnlyList<TenantUserDto>>
{
    private readonly ITenantUserQueries _users;

    public GetTenantUsersQueryHandler(ITenantUserQueries users)
    {
        _users = users;
    }

    public Task<IReadOnlyList<TenantUserDto>> Handle(
        GetTenantUsersQuery request, CancellationToken cancellationToken) =>
        // No tenant filter is written anywhere in this path — the membership query filter
        // does it. Another pharmacy's rows and the platform admins' null-tenant rows are
        // both absent by construction.
        _users.ListForCurrentTenantAsync(cancellationToken);
}
