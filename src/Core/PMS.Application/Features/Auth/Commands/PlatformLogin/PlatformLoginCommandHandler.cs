using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Auth.Commands.PlatformLogin;

public sealed class PlatformLoginCommandHandler
    : IRequestHandler<PlatformLoginCommand, Result<AuthResultDto>>
{
    // One message for every failure. Distinguishing "no such account" from "wrong password"
    // from "you are not a platform operator" would tell an attacker which is which.
    //
    // The log separates them, for the operator only. See TenantLoginCommandHandler for the
    // reasoning; it applies with more force here, because this endpoint is the one worth
    // attacking and a run of refusals against it is something somebody should notice.
    private static readonly Error InvalidCredentials =
        Error.Unauthorized("Invalid credentials.");

    private readonly IIdentityQueries _identity;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _tokens;
    private readonly ILogger<PlatformLoginCommandHandler> _logger;

    public PlatformLoginCommandHandler(
        IIdentityQueries identity,
        IPasswordHasher hasher,
        IJwtTokenService tokens,
        ILogger<PlatformLoginCommandHandler> logger)
    {
        _identity = identity;
        _hasher = hasher;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<Result<AuthResultDto>> Handle(
        PlatformLoginCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim() ?? string.Empty;

        // Sanctioned filter exception 2: identity is global, not tenant-scoped.
        var user = await _identity.FindUserByEmailAsync(request.Email, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning(
                "Platform login refused: no account exists for {Email}", email);

            return Result.Failure<AuthResultDto>(InvalidCredentials);
        }

        if (!user.IsGloballyActive)
        {
            _logger.LogWarning(
                "Platform login refused for {Email}: the account is disabled platform-wide",
                email);

            return Result.Failure<AuthResultDto>(InvalidCredentials);
        }

        if (!_hasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning(
                "Platform login refused for {Email}: wrong password", email);

            return Result.Failure<AuthResultDto>(InvalidCredentials);
        }

        // Sanctioned filter exception 3: a platform membership has no tenant, so no tenant
        // context could ever see it.
        var membership = await _identity.FindPlatformMembershipAsync(user.Id, cancellationToken);

        if (membership is null)
        {
            // Worth its own line and worth reading twice. This is a real pharmacy user, with
            // the correct password, asking for a token that governs every pharmacy on the
            // platform. Once it is a mis-click on the wrong login page; repeatedly, from one
            // account, it is somebody trying the door.
            _logger.LogWarning(
                "Platform login refused for {Email}: correct password, but this account is "
                + "not a platform operator (user {UserId})",
                email, user.Id);

            return Result.Failure<AuthResultDto>(InvalidCredentials);
        }

        if (!membership.IsActive)
        {
            _logger.LogWarning(
                "Platform login refused for {Email}: platform access was revoked (user {UserId})",
                email, user.Id);

            return Result.Failure<AuthResultDto>(InvalidCredentials);
        }

        var token = _tokens.IssueForPlatformAdmin(user.Id);

        // The most consequential success the system can record: this token is outside every
        // tenant filter, so it can read and change any pharmacy. Every platform sign-in
        // belongs on file, whether or not anything came of it.
        _logger.LogInformation(
            "Platform login succeeded for {Email} (user {UserId}, token expires "
            + "{ExpiresAtUtc:u}) — this session is not scoped to any pharmacy",
            email, user.Id, token.ExpiresAtUtc);

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
