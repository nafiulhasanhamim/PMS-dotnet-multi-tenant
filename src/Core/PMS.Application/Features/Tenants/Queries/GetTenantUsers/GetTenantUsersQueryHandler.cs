using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using MediatR;

namespace PMS.Application.Features.Tenants.Queries.GetTenantUsers;

public sealed class GetTenantUsersQueryHandler
    : IRequestHandler<GetTenantUsersQuery, IReadOnlyList<TenantUserDto>>
{
    private readonly IIdentityQueries _identity;

    public GetTenantUsersQueryHandler(IIdentityQueries identity)
    {
        _identity = identity;
    }

    public async Task<IReadOnlyList<TenantUserDto>> Handle(
        GetTenantUsersQuery request, CancellationToken cancellationToken)
    {
        var memberships = await _identity.ListMembershipsForTenantAsync(
            request.TenantId, cancellationToken);

        return memberships
            .Select(m => new TenantUserDto(m.Id, m.UserId, m.User.Email, m.User.FullName,
                m.Role, m.IsActive, m.JoinedAt))
            .ToList();
    }
}
