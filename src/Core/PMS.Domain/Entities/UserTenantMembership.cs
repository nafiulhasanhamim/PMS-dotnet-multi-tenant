using Ardalis.GuardClauses;
using PMS.Domain.Enums;
using PMS.SharedKernel.Common;

namespace PMS.Domain.Entities;

/// <summary>
/// What a user may do, and where. The bridge between a global identity and a pharmacy.
///
/// A membership with a tenant is pharmacy staff. A membership with **no** tenant is a
/// platform operator — that is the whole of what makes someone a platform admin, which is
/// why there is no second identity table for them.
///
/// Deliberately not an ITenantEntity, even though it has a TenantId. That marker means "this
/// row belongs to exactly one pharmacy and is filtered automatically", and neither half is
/// true here: the tenant is nullable, and the row has to be readable at login before any
/// tenant context exists. It gets its own hand-written query filter instead — see
/// ApplicationDbContext.
/// </summary>
public sealed class UserTenantMembership : BaseAuditableAggregateRoot<Guid>
{
    /// <summary>
    /// The pharmacy this membership is for, or null for a platform operator.
    ///
    /// A database check constraint keeps this in step with <see cref="Role"/>: null if and
    /// only if the role is PlatformAdmin.
    /// </summary>
    public Guid? TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public UserRole Role { get; private set; }

    /// <summary>
    /// Whether this membership grants access. Revoking it locks the person out of *this*
    /// pharmacy only — their identity and any other memberships are untouched.
    /// </summary>
    public bool IsActive { get; private set; }

    public DateTime JoinedAt { get; private set; }

    public Tenant? Tenant { get; private set; }
    public User User { get; private set; } = null!;

    private UserTenantMembership() { }

    private UserTenantMembership(Guid? tenantId, Guid userId, UserRole role)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        UserId = userId;
        Role = role;
        IsActive = true;
        JoinedAt = DateTime.UtcNow;
    }

    /// <summary>Grants someone a role at one pharmacy.</summary>
    public static UserTenantMembership ForTenant(Guid tenantId, Guid userId, UserRole role)
    {
        Guard.Against.Default(tenantId, nameof(tenantId));
        Guard.Against.Default(userId, nameof(userId));

        if (role == UserRole.PlatformAdmin)
        {
            throw new ArgumentException(
                "PlatformAdmin is a platform-level role and cannot be granted at a pharmacy. " +
                "Use ForPlatform instead.", nameof(role));
        }

        return new UserTenantMembership(tenantId, userId, role);
    }

    /// <summary>Makes someone a platform operator: a membership with no pharmacy.</summary>
    public static UserTenantMembership ForPlatform(Guid userId)
    {
        Guard.Against.Default(userId, nameof(userId));
        return new UserTenantMembership(null, userId, UserRole.PlatformAdmin);
    }

    /// <summary>True when this is a platform-level membership rather than a pharmacy one.</summary>
    public bool IsPlatformLevel => TenantId is null;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
