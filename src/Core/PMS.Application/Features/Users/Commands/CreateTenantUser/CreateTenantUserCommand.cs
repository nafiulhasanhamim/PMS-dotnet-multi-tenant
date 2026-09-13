using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Users.Commands.CreateTenantUser;

/// <summary>
/// Adds staff to the caller's *own* pharmacy.
///
/// Note there is no TenantId: it comes from the token via ICurrentTenantService, never from
/// the request, so an Admin cannot provision into someone else's pharmacy.
/// </summary>
public sealed record CreateTenantUserCommand(
    string Email, string FullName, string Password, UserRole Role)
    : IRequest<Result<ProvisionedUserDto>>, ITenantScopedRequest;
