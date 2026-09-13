using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Common.Provisioning;

public enum PlatformAdminSeedOutcome
{
    /// <summary>No account existed for that email; one was created.</summary>
    Created = 0,

    /// <summary>The email already had an account; it was given platform access.</summary>
    PromotedExistingUser = 1,

    /// <summary>That email is already a platform administrator; nothing was changed.</summary>
    AlreadyPresent = 2,
}

public sealed record PlatformAdminSeedResult(PlatformAdminSeedOutcome Outcome, Guid UserId, string Email);

/// <summary>
/// Creates the first platform administrator, so the system can be signed into at all.
///
/// This exists because of a genuine chicken-and-egg problem: pharmacies are created by a
/// platform administrator, and platform administrators are created by... nothing. Every other
/// account in the system has a creator. This one cannot.
///
/// Two deliberate limits keep that from becoming a back door:
///
///  * It never touches an existing password. If the email already has an account it is granted
///    platform access and keeps the credentials it already had — the same rule
///    <see cref="UserProvisioner"/> follows, for the same reason.
///  * It never creates a *second* platform administrator. Once one exists, the seeder is a
///    no-op for that email; further ones are made through the platform API by someone who is
///    already signed in. So leaving the seed configuration in place does not leave a way to
///    mint administrators by editing a config file.
/// </summary>
public sealed class PlatformAdminSeeder
{
    private readonly IIdentityQueries _identity;
    private readonly IRepository<User, IApplicationDbContext> _users;
    private readonly IRepository<UserTenantMembership, IApplicationDbContext> _memberships;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly IPasswordHasher _hasher;
    private readonly ILogger<PlatformAdminSeeder> _logger;

    public PlatformAdminSeeder(
        IIdentityQueries identity,
        IRepository<User, IApplicationDbContext> users,
        IRepository<UserTenantMembership, IApplicationDbContext> memberships,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        IPasswordHasher hasher,
        ILogger<PlatformAdminSeeder> logger)
    {
        _identity = identity;
        _users = users;
        _memberships = memberships;
        _unitOfWork = unitOfWork;
        _hasher = hasher;
        _logger = logger;
    }

    public async Task<PlatformAdminSeedResult> EnsureAsync(
        string email, string fullName, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("A platform administrator needs an email.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("A platform administrator needs a password.", nameof(password));
        }

        var existing = await _identity.FindUserByEmailAsync(email, cancellationToken);

        if (existing is not null)
        {
            // The membership lookup has to bypass the tenant filter — a platform membership has
            // no tenant to be matched by — which is why it goes through IIdentityQueries.
            var already = await _identity.FindPlatformMembershipAsync(existing.Id, cancellationToken);

            if (already is not null)
            {
                _logger.LogInformation(
                    "Platform administrator {Email} already exists; seeding skipped.", existing.Email);

                return new PlatformAdminSeedResult(
                    PlatformAdminSeedOutcome.AlreadyPresent, existing.Id, existing.Email);
            }

            await _memberships.AddAsync(
                UserTenantMembership.ForPlatform(existing.Id), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Existing account {Email} was granted platform administrator access by seeding. "
                + "Its password was not changed.", existing.Email);

            return new PlatformAdminSeedResult(
                PlatformAdminSeedOutcome.PromotedExistingUser, existing.Id, existing.Email);
        }

        var user = new User(email, fullName, _hasher.Hash(password));
        await _users.AddAsync(user, cancellationToken);
        await _memberships.AddAsync(UserTenantMembership.ForPlatform(user.Id), cancellationToken);

        // One transaction: an identity with no platform membership could not sign in anywhere,
        // and would then block a later seeding attempt by occupying the email.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Platform administrator {Email} created by seeding. Change this password before "
            + "the system is used for real.", user.Email);

        return new PlatformAdminSeedResult(PlatformAdminSeedOutcome.Created, user.Id, user.Email);
    }
}
