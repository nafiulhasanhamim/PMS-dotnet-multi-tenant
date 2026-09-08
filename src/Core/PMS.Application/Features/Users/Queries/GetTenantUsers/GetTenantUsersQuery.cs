using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using MediatR;

namespace PMS.Application.Features.Users.Queries.GetTenantUsers;

/// <summary>The caller's own pharmacy's staff. Scoped by the membership query filter.</summary>
public sealed record GetTenantUsersQuery
    : IRequest<IReadOnlyList<TenantUserDto>>, ITenantScopedRequest;
