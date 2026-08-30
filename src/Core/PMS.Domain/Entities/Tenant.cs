using Ardalis.GuardClauses;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;

namespace PMS.Domain.Entities;

/// <summary>
/// One pharmacy using the system.
///
/// The tenant root itself, so it deliberately does *not* implement ITenantEntity — it is
/// the thing other rows point at, and it is managed by a platform administrator rather
/// than from inside any tenant.
/// </summary>
public sealed class Tenant : BaseAuditableAggregateRoot<Guid>, ISoftDelete
{
    /// <summary>
    /// The pharmacy's name, as printed on its invoices.
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// Short unique key for the pharmacy, e.g. "citycare". Lowercase, no spaces.
    /// Kept separate from the display name so the name can be corrected without
    /// invalidating anything that references the tenant.
    /// </summary>
    public string Slug { get; private set; } = null!;

    /// <summary>
    /// Suspends a pharmacy without deleting it. An inactive tenant's users cannot sign
    /// in, but its data is untouched.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTime? DeletedOnUtc { get; set; }

    /// <inheritdoc />
    public string? DeletedBy { get; set; }

    private Tenant() { }

    public Tenant(string name, string slug)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.NullOrWhiteSpace(slug, nameof(slug));

        Id = Guid.NewGuid();
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        IsActive = true;
    }

    public void Rename(string name)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Name = name.Trim();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
