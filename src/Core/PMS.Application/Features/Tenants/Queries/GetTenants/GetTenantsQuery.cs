using PMS.Application.Common.DTOs;
using MediatR;

namespace PMS.Application.Features.Tenants.Queries.GetTenants;

/// <summary>Every pharmacy, whatever its status. Platform-level.</summary>
public sealed record GetTenantsQuery : IRequest<IReadOnlyList<TenantDto>>;
