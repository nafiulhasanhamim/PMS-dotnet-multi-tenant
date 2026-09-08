using Ardalis.GuardClauses;
using PMS.SharedKernel.Common;

namespace PMS.Domain.Entities;

/// <summary>
/// One person, once, for the whole platform.
///
/// There is a single identity table for everybody — platform operators and pharmacy staff
/// alike. What a person may do, and where, is decided entirely by their
/// <see cref="UserTenantMembership"/> rows; nothing on this entity says anything about a
/// role or a pharmacy.
///
/// That is what lets the same person work at two pharmacies with one email and one password,
/// holding a different role in each.
///
/// Deliberately not an ITenantEntity: an identity is global, and the tenant filter would make
/// it impossible to find a user at login, before any tenant is known.
/// </summary>
public sealed class User : BaseAuditableAggregateRoot<Guid>
{
    /// <summary>The identity anchor. Unique across the whole platform, not per pharmacy.</summary>
    public string Email { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public string FullName { get; private set; } = null!;

    /// <summary>
    /// A platform-level kill switch, independent of any membership. Turning this off locks
    /// the person out of every pharmacy at once; deactivating a single membership only
    /// affects that one.
    /// </summary>
    public bool IsGloballyActive { get; private set; }

    private readonly List<UserTenantMembership> _memberships = [];
    public IReadOnlyCollection<UserTenantMembership> Memberships => _memberships.AsReadOnly();

    private User() { }

    public User(string email, string fullName, string passwordHash)
    {
        Guard.Against.NullOrWhiteSpace(email, nameof(email));
        Guard.Against.NullOrWhiteSpace(fullName, nameof(fullName));
        Guard.Against.NullOrWhiteSpace(passwordHash, nameof(passwordHash));

        Id = Guid.NewGuid();
        Email = NormalizeEmail(email);
        FullName = fullName.Trim();
        PasswordHash = passwordHash;
        IsGloballyActive = true;
    }

    public void SetPasswordHash(string passwordHash)
    {
        Guard.Against.NullOrWhiteSpace(passwordHash, nameof(passwordHash));
        PasswordHash = passwordHash;
    }

    public void Rename(string fullName)
    {
        Guard.Against.NullOrWhiteSpace(fullName, nameof(fullName));
        FullName = fullName.Trim();
    }

    public void ActivateGlobally() => IsGloballyActive = true;

    public void DeactivateGlobally() => IsGloballyActive = false;

    /// <summary>Case-folded, so one person cannot end up with two accounts.</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
