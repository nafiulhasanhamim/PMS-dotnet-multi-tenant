using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Users.Queries.GetMyProfile;

public sealed class GetMyProfileQueryHandler
    : IRequestHandler<GetMyProfileQuery, Result<MyProfileDto>>
{
    private readonly ICurrentUserService _user;
    private readonly ICurrentTenantService _tenant;
    private readonly IIdentityQueries _identity;
    private readonly ILogger<GetMyProfileQueryHandler> _logger;

    public GetMyProfileQueryHandler(
        ICurrentUserService user,
        ICurrentTenantService tenant,
        IIdentityQueries identity,
        ILogger<GetMyProfileQueryHandler> logger)
    {
        _user = user;
        _tenant = tenant;
        _identity = identity;
        _logger = logger;
    }

    public async Task<Result<MyProfileDto>> Handle(
        GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var userId = _user.UserGuid;
        if (userId is null)
        {
            // A token that authenticated but carries no usable user id — a malformed or
            // stale claim set rather than an expired session, and a bug rather than a
            // refusal, since nothing should have got this far without one.
            _logger.LogWarning(
                "Profile refused: the request authenticated but carries no user id claim");

            return Result.Failure<MyProfileDto>(Error.Unauthorized("Not signed in."));
        }

        var user = await _identity.FindUserByIdAsync(userId.Value, cancellationToken);
        var tenant = await _identity.FindTenantByIdAsync(_tenant.TenantId, cancellationToken);

        // The role is read from the membership rather than the token, so revoking access
        // takes effect on the next call instead of when the token expires.
        var membership = await _identity.FindMembershipAsync(
            _tenant.TenantId, userId.Value, cancellationToken);

        if (user is null || tenant is null || membership is null || !membership.IsActive)
        {
            // This is where a revoked membership actually bites, and how it reaches the
            // person is as a sudden sign-out in the middle of what they were doing. The
            // branches stay collapsed for the response but the log names which one it was,
            // because "my session died" has four different causes here and only one of them
            // (the revocation) is somebody having done it on purpose.
            _logger.LogWarning(
                "Profile refused for user {UserId} at tenant {TenantId}: {Reason}",
                userId.Value,
                _tenant.TenantId,
                user is null ? "the account no longer exists"
                    : tenant is null ? "the pharmacy no longer exists"
                    : membership is null ? "no membership at this pharmacy"
                    : "the membership has been revoked");

            return Result.Failure<MyProfileDto>(
                Error.Unauthorized("Your access to this pharmacy is no longer valid."));
        }

        // Debug, deliberately: the frontend calls this on nearly every page to render the
        // header, so at Information it would bury the lines that matter. The refusal above
        // is what is worth keeping, and it stays at Warning.
        _logger.LogDebug(
            "Profile served for {Email} at {TenantName} as {Role}",
            user.Email, tenant.Name, membership.Role);

        return new MyProfileDto(
            user.Id, user.Email, user.FullName, membership.Role,
            new TenantSummaryDto(tenant.Id, tenant.Name, tenant.DomainName));
    }
}
