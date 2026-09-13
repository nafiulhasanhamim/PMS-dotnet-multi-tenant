using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Users.Commands.SetMembershipActive;

/// <summary>
/// Revokes or restores one person's access to the caller's pharmacy.
///
/// Affects that membership only — their identity and any access they hold at other
/// pharmacies are untouched.
/// </summary>
public sealed record SetMembershipActiveCommand(Guid MembershipId, bool IsActive)
    : IRequest<Result<TenantUserDto>>, ITenantScopedRequest;
