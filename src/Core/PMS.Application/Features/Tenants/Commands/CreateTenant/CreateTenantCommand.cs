using PMS.Application.Common.DTOs;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Tenants.Commands.CreateTenant;

/// <summary>Onboards a pharmacy. Platform-level: deliberately not ITenantScopedRequest.</summary>
public sealed record CreateTenantCommand(string Name, string DomainName, string? SubscriptionPlan)
    : IRequest<Result<TenantDto>>;
