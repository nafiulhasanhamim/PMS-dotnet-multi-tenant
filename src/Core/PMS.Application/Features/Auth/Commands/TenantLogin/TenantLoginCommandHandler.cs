using PMS.Application.Common.DTOs;
using PMS.Application.Common.Tenancy;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Auth.Commands.TenantLogin;

public sealed class TenantLoginCommandHandler
    : IRequestHandler<TenantLoginCommand, Result<AuthResultDto>>
{
    // Every failure below returns this. Unknown domain, suspended pharmacy, unknown email,
    // wrong password, no membership, revoked membership — all identical. Telling them apart
    // would reveal which pharmacies exist and who works at them.
    private static readonly Error InvalidCredentials =
        Error.Unauthorized("Invalid credentials.");

    private readonly IIdentityQueries _identity;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _tokens;
    private readonly TenancySettings _tenancy;

    public TenantLoginCommandHandler(
        IIdentityQueries identity,
        IPasswordHasher hasher,
        IJwtTokenService tokens,
        TenancySettings tenancy)
    {
        _identity = identity;
        _hasher = hasher;
        _tokens = tokens;
        _tenancy = tenancy;
    }

    public async Task<Result<AuthResultDto>> Handle(
        TenantLoginCommand request, CancellationToken cancellationToken)
    {
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
        if (tenant is null || !tenant.CanBeUsed)
        {
            return Result.Failure<AuthResultDto>(InvalidCredentials);
        }

        // 2. Find the person. Exception 2 — identity is global.
        var user = await _identity.FindUserByEmailAsync(request.Email, cancellationToken);
        if (user is null || !user.IsGloballyActive
            || !_hasher.Verify(request.Password, user.PasswordHash))
        {
            return Result.Failure<AuthResultDto>(InvalidCredentials);
        }

        // 3. Do they have access *here*? Exception 3. The role comes from this row, not from
        //    anything on the user — which is how the same person can be a Pharmacist at one
        //    pharmacy and an Employee at another.
        var membership = await _identity.FindMembershipAsync(tenant.Id, user.Id, cancellationToken);
        if (membership is null || !membership.IsActive)
        {
            return Result.Failure<AuthResultDto>(InvalidCredentials);
        }

        var token = _tokens.IssueForTenantUser(user.Id, tenant.Id, membership.Role);

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
