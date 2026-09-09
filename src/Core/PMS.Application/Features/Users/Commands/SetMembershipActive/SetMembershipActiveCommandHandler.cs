using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Users.Commands.SetMembershipActive;

public sealed class SetMembershipActiveCommandHandler
    : IRequestHandler<SetMembershipActiveCommand, Result<TenantUserDto>>
{
    private readonly ITenantUserQueries _users;
    private readonly ILogger<SetMembershipActiveCommandHandler> _logger;

    public SetMembershipActiveCommandHandler(
        ITenantUserQueries users, ILogger<SetMembershipActiveCommandHandler> logger)
    {
        _users = users;
        _logger = logger;
    }

    public async Task<Result<TenantUserDto>> Handle(
        SetMembershipActiveCommand request, CancellationToken cancellationToken)
    {
        // Read through the filter, so a membership belonging to another pharmacy is simply
        // not found. That is why this is a 404 rather than a 403 — from inside this pharmacy
        // the row genuinely does not exist.
        var updated = await _users.SetActiveAsync(
            request.MembershipId, request.IsActive, cancellationToken);

        if (updated is null)
        {
            // Worth saying out loud that this covers two different situations, because the
            // response cannot: a membership that does not exist, and one that exists at
            // another pharmacy and is hidden by the query filter. If a caller swears the id
            // is real, the second is why — and that would be a cross-tenant attempt.
            _logger.LogWarning(
                "Membership {MembershipId} not updated: not found in this pharmacy "
                + "(it may belong to another one)",
                request.MembershipId);

            return Result.Failure<TenantUserDto>(
                Error.NotFound(nameof(UserTenantMembership), request.MembershipId));
        }

        // Revoking access takes effect on the next call, because GetMyProfileQueryHandler
        // reads the role from the membership rather than the token. To the person affected
        // that is an abrupt sign-out with no explanation, so the deliberate act gets a line.
        _logger.LogInformation(
            "Membership {MembershipId} {Action} for {Email} ({Role})",
            updated.MembershipId,
            request.IsActive ? "reactivated" : "revoked",
            updated.Email,
            updated.Role);

        return Result.Success(updated);
    }
}
