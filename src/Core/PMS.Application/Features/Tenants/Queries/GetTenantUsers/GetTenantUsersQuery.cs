using PMS.Application.Common.DTOs;
using MediatR;

namespace PMS.Application.Features.Tenants.Queries.GetTenantUsers;

/// <summary>
/// A named pharmacy's staff, read from outside it.
///
/// Distinct from <c>GetUsersQuery</c>, which takes no tenant id because it reads whichever
/// pharmacy the caller's token belongs to. This one is for a platform operator, who belongs
/// to none — so the tenant is a parameter, and that makes it platform-only by construction.
/// </summary>
public sealed record GetTenantUsersQuery(Guid TenantId) : IRequest<IReadOnlyList<TenantUserDto>>;
