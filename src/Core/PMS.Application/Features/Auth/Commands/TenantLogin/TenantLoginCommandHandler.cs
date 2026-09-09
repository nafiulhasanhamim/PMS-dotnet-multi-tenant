using PMS.Application.Common.DTOs;
using PMS.Application.Common.Tenancy;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Auth.Commands.TenantLogin;

public sealed class TenantLoginCommandHandler
    : IRequestHandler<TenantLoginCommand, Result<AuthResultDto>>
{
    // Every failure below returns this. Unknown domain, suspended pharmacy, unknown email,
    // wrong password, no membership, revoked membership — all identical. Telling them apart
    // would reveal which pharmacies exist and who works at them.
    //
    // The log does tell them apart, and the asymmetry is the point: whoever reads a file on
    // the server has already been trusted, and the caller has not. Otherwise "I cannot log
    // in" is unanswerable — seven causes, one message, and no way to know which occurred.
    private static readonly Error InvalidCredentials =
        Error.Unauthorized("Invalid credentials.");

    private readonly IIdentityQueries _identity;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _tokens;
    private readonly TenancySettings _tenancy;
    private readonly ILogger<TenantLoginCommandHandler> _logger;

    public TenantLoginCommandHandler(
        IIdentityQueries identity,
        IPasswordHasher hasher,
        IJwtTokenService tokens,
        TenancySettings tenancy,
        ILogger<TenantLoginCommandHandler> logger)
    {
        _identity = identity;
        _hasher = hasher;
        _tokens = tokens;
        _tenancy = tenancy;
        _logger = logger;
    }

    public async Task<Result<AuthResultDto>> Handle(
        TenantLoginCommand request, CancellationToken cancellationToken)
    {
        // Used only in log lines. The lookups below keep reading request.Email, so nothing
        // about how a user is found depends on a value prepared for a log file.
        var email = request.Email?.Trim() ?? string.Empty;

        // 1. Resolve the pharmacy. Sanctioned exception 1 — no tenant context exists yet;
        //    this is the call that establishes it.
        //
        //    What arrives here is either a browser host (when the address named the pharmacy)
        //    or whatever someone typed into the login form. Both go through the same rule:
        //    an exact match on the whole address first, so a pharmacy that owns its own domain
        //    is found by it, then the leading label under the platform's base domain. See
        //    Tenant.ResolutionCandidates for why the order is what makes one column holding
        //    both kinds of value unambiguous.
        var tenant = await ResolveTenantAsync(request.DomainName, cancellationToken);

        if (tenant is null)
        {
            // Records the address exactly as it arrived, because the address is the thing
            // that was wrong. A typo in a subdomain and a pharmacy that was never
            // provisioned are indistinguishable to the user and one glance apart here.
            _logger.LogWarning(
                "Tenant login refused for {Email}: no pharmacy is registered at {Address}",
                email,
                string.IsNullOrWhiteSpace(request.DomainName)
                    ? "(no address supplied)"
                    : request.DomainName);

            return Result.Failure<AuthResultDto>(InvalidCredentials);
        }

        if (!tenant.CanBeUsed)
        {
            // Split out from the null check for the sake of this line. It is the one failure
            // here that is nobody's mistake: without it, a pharmacy locked out by a
            // suspension looks exactly like its entire staff forgetting their passwords at
            // the same moment.
            _logger.LogWarning(
                "Tenant login refused for {Email}: pharmacy {TenantName} is {TenantStatus}",
                email, tenant.Name, tenant.Status);

            return Result.Failure<AuthResultDto>(InvalidCredentials);
        }

        // 2. Find the person. Exception 2 — identity is global.
        var user = await _identity.FindUserByEmailAsync(request.Email, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning(
                "Tenant login refused: no account exists for {Email} (pharmacy {TenantName})",
                email, tenant.Name);

            return Result.Failure<AuthResultDto>(InvalidCredentials);
        }

        if (!user.IsGloballyActive)
        {
            // Distinct from the revoked membership below, and the distinction is actionable:
            // this account is dead everywhere, so granting it access to another pharmacy
            // would not fix anything.
            _logger.LogWarning(
                "Tenant login refused for {Email}: the account is disabled platform-wide",
                email);

            return Result.Failure<AuthResultDto>(InvalidCredentials);
        }

        if (!_hasher.Verify(request.Password, user.PasswordHash))
        {
            // The attempted password is deliberately absent. A log file is not a place to
            // put one, and a near-miss hint would make this file worth stealing.
            _logger.LogWarning(
                "Tenant login refused for {Email}: wrong password (pharmacy {TenantName})",
                email, tenant.Name);

            return Result.Failure<AuthResultDto>(InvalidCredentials);
        }

        // 3. Do they have access *here*? Exception 3. The role comes from this row, not from
        //    anything on the user — which is how the same person can be a Pharmacist at one
        //    pharmacy and an Employee at another.
        var membership = await _identity.FindMembershipAsync(tenant.Id, user.Id, cancellationToken);

        if (membership is null)
        {
            // The confusing case, and on its own reason enough to split these branches: the
            // credentials are correct, just not for this pharmacy. Someone who works at two
            // branches and signs in at the wrong address is told "invalid credentials" and
            // will insist their password works — because it does, next door.
            _logger.LogWarning(
                "Tenant login refused for {Email}: credentials are valid but there is no "
                + "membership at {TenantName}",
                email, tenant.Name);

            return Result.Failure<AuthResultDto>(InvalidCredentials);
        }

        if (!membership.IsActive)
        {
            _logger.LogWarning(
                "Tenant login refused for {Email}: membership at {TenantName} was revoked",
                email, tenant.Name);

            return Result.Failure<AuthResultDto>(InvalidCredentials);
        }

        var token = _tokens.IssueForTenantUser(user.Id, tenant.Id, membership.Role);

        // Successes matter as much as failures: this is the line that answers "who was
        // signed in when that happened?". The role is on it because the role is what every
        // later authorization decision turns on, and the ids because they are what the rest
        // of the log correlates by.
        _logger.LogInformation(
            "Tenant login succeeded for {Email} at {TenantName} as {Role} "
            + "(user {UserId}, tenant {TenantId}, token expires {ExpiresAtUtc:u})",
            email, tenant.Name, membership.Role, user.Id, tenant.Id, token.ExpiresAtUtc);

        return new AuthResultDto(
            token.Token,
            token.ExpiresAtUtc,
            user.Id,
            user.Email,
            user.FullName,
            new TenantSummaryDto(tenant.Id, tenant.Name, tenant.DomainName),
            membership.Role);
    }

    private async Task<Tenant?> ResolveTenantAsync(
        string? domainName, CancellationToken cancellationToken)
    {
        foreach (var candidate in Tenant.ResolutionCandidates(domainName, _tenancy.BaseDomain))
        {
            var tenant = await _identity.FindTenantByDomainAsync(candidate, cancellationToken);

            if (tenant is not null)
            {
                return tenant;
            }
        }

        return null;
    }
}
