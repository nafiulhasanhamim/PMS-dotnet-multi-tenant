using PMS.Application.Common.DTOs;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Tenants.Queries.GetTenant;

/// <summary>One pharmacy by id, whatever its status. Platform-level.</summary>
public sealed record GetTenantQuery(Guid TenantId) : IRequest<Result<TenantDto>>;
