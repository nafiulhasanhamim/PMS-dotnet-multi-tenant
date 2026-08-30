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
    /// The pharmacy's own domain, e.g. "citycare.com" or "citycare.pms.app".
    ///
    /// Optional: a pharmacy can run perfectly well without one. When set it must be unique,
    /// because a host that resolved to two tenants could not be resolved at all.
    ///
    /// Stored as a bare lowercase host — no scheme, port or path — so that comparing an
    /// incoming request's host against it is a plain string match.
    /// </summary>
    public string? DomainName { get; private set; }

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

    public Tenant(string name, string slug, string? domainName = null)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.NullOrWhiteSpace(slug, nameof(slug));

        Id = Guid.NewGuid();
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        DomainName = NormalizeDomain(domainName);
        IsActive = true;
    }

    public void Rename(string name)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Name = name.Trim();
    }

    /// <summary>
    /// Sets or clears the pharmacy's domain. Pass null or blank to remove it.
    /// </summary>
    public void SetDomainName(string? domainName) => DomainName = NormalizeDomain(domainName);

    /// <summary>
    /// Reduces whatever was typed to a bare lowercase host.
    ///
    /// People paste what is in their address bar, so "https://CityCare.com/login" and
    /// "citycare.com" have to end up as the same value — otherwise two tenants could hold
    /// what is really the same domain and the unique index would not notice.
    /// </summary>
    private static string? NormalizeDomain(string? domainName)
    {
        if (string.IsNullOrWhiteSpace(domainName))
        {
            return null;
        }

        var host = domainName.Trim().ToLowerInvariant();

        var scheme = host.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            host = host[(scheme + 3)..];
        }

        // drop any path, query or port
        host = host.Split('/', '?', '#', ':')[0].Trim();

        return host.Length == 0 ? null : host;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
