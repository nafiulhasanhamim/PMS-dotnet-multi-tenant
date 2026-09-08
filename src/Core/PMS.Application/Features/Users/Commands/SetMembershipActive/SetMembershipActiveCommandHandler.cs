using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Users.Commands.SetMembershipActive;

public sealed class SetMembershipActiveCommandHandler
    : IRequestHandler<SetMembershipActiveCommand, Result<TenantUserDto>>
{
    private readonly ITenantUserQueries _users;

    public SetMembershipActiveCommandHandler(ITenantUserQueries users)
    {
        _users = users;
    }

    public async Task<Result<TenantUserDto>> Handle(
        SetMembershipActiveCommand request, CancellationToken cancellationToken)
    {
        // Read through the filter, so a membership belonging to another pharmacy is simply
        // not found. That is why this is a 404 rather than a 403 — from inside this pharmacy
        // the row genuinely does not exist.
        var updated = await _users.SetActiveAsync(
            request.MembershipId, request.IsActive, cancellationToken);

        return updated is null
            ? Result.Failure<TenantUserDto>(
                Error.NotFound(nameof(UserTenantMembership), request.MembershipId))
            : Result.Success(updated);
    }
}
