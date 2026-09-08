using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Auth.Commands.PlatformLogin;

public sealed class PlatformLoginCommandHandler
    : IRequestHandler<PlatformLoginCommand, Result<AuthResultDto>>
{
    // One message for every failure. Distinguishing "no such account" from "wrong password"
    // from "you are not a platform operator" would tell an attacker which is which.
    private static readonly Error InvalidCredentials =
        Error.Unauthorized("Invalid credentials.");

    private readonly IIdentityQueries _identity;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _tokens;

    public PlatformLoginCommandHandler(
        IIdentityQueries identity, IPasswordHasher hasher, IJwtTokenService tokens)
    {
        _identity = identity;
        _hasher = hasher;
        _tokens = tokens;
    }

    public async Task<Result<AuthResultDto>> Handle(
        PlatformLoginCommand request, CancellationToken cancellationToken)
    {
        // Sanctioned filter exception 2: identity is global, not tenant-scoped.
        var user = await _identity.FindUserByEmailAsync(request.Email, cancellationToken);

        if (user is null || !user.IsGloballyActive
            || !_hasher.Verify(request.Password, user.PasswordHash))
        {
            return Result.Failure<AuthResultDto>(InvalidCredentials);
        }

        // Sanctioned filter exception 3: a platform membership has no tenant, so no tenant
        // context could ever see it.
        var membership = await _identity.FindPlatformMembershipAsync(user.Id, cancellationToken);

        if (membership is null || !membership.IsActive)
        {
            return Result.Failure<AuthResultDto>(InvalidCredentials);
        }

        var token = _tokens.IssueForPlatformAdmin(user.Id);

        return new AuthResultDto(
            token.Token,
            token.ExpiresAtUtc,
            user.Id,
            user.Email,
            user.FullName,
            // Deliberately no tenant: a platform token is outside every pharmacy.
            Tenant: null,
            UserRole.PlatformAdmin);
    }
}
