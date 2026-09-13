using PMS.Application.Common.DTOs;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Tenants.Commands.CreateTenantAdmin;

/// <summary>
/// The only route by which a pharmacy gets its first user. Platform-level: the tenant comes
/// from the route, not from a token, because the caller is outside every pharmacy.
/// </summary>
public sealed record CreateTenantAdminCommand(
    Guid TenantId, string Email, string FullName, string Password)
    : IRequest<Result<ProvisionedUserDto>>;
