using Ardalis.GuardClauses;
using PMS.Domain.Enums;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;

namespace PMS.Domain.Entities;

/// <summary>
/// One pharmacy using the system.
///
/// The tenant root, so it deliberately does not implement ITenantEntity — it is the thing
/// other rows point at, and it is managed by a platform administrator rather than from
/// inside any tenant.
/// </summary>
public sealed class Tenant : BaseAuditableAggregateRoot<Guid>, ISoftDelete
{
    /// <summary>The pharmacy's business name, as printed on its invoices.</summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// The key a login is addressed to, e.g. "popular-pharmacy". Unique across the platform.
    ///
    /// This is what removes the "which pharmacy?" step from signing in: a user posts their
    /// domain with their credentials, and that alone decides which tenant — and therefore
    /// which role — the session gets. Stored as a bare lowercase host so that "citycare.com"
    /// and "https://CityCare.com/login" are recognised as the same pharmacy.
    /// </summary>
    public string DomainName { get; private set; } = null!;

    /// <summary>
    /// Whether the pharmacy may be used. Suspension withdraws access; it deletes nothing,
    /// and the pharmacy's data is untouched and restored intact on reactivation.
    /// </summary>
    public TenantStatus Status { get; private set; }

    /// <summary>
    /// The plan this pharmacy is on. Recorded only — no billing or entitlement logic yet.
    /// </summary>
    public string? SubscriptionPlan { get; private set; }

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTime? DeletedOnUtc { get; set; }

    /// <inheritdoc />
    public string? DeletedBy { get; set; }

    private Tenant() { }

    public Tenant(string name, string domainName, string? subscriptionPlan = null)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.NullOrWhiteSpace(domainName, nameof(domainName));

        Id = Guid.NewGuid();
        Name = name.Trim();
        DomainName = NormalizeDomain(domainName)
            ?? throw new ArgumentException("A domain name is required.", nameof(domainName));
        SubscriptionPlan = subscriptionPlan?.Trim();
        // New pharmacies start usable; nothing here decides when a trial ends.
        Status = TenantStatus.Trial;
    }

    /// <summary>True when users of this pharmacy are allowed to sign in.</summary>
    public bool CanBeUsed => Status != TenantStatus.Suspended && !IsDeleted;

    public void Rename(string name)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Name = name.Trim();
    }

    public void SetDomainName(string domainName)
    {
        DomainName = NormalizeDomain(domainName)
            ?? throw new ArgumentException("A domain name is required.", nameof(domainName));
    }

    public void SetStatus(TenantStatus status) => Status = status;

    public void SetSubscriptionPlan(string? plan) => SubscriptionPlan = plan?.Trim();

    /// <summary>
    /// Reduces whatever was typed to a bare lowercase host.
    ///
    /// People paste what is in their address bar, so "https://CityCare.com/login" and
    /// "citycare.com" have to end up as the same value — otherwise two pharmacies could hold
    /// what is really the same domain and the unique index would not notice.
    /// </summary>
    public static string? NormalizeDomain(string? domainName)
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

        host = host.Split('/', '?', '#', ':')[0].Trim();

        // A trailing dot is the DNS root and is legal in a hostname — "citycare.com." and
        // "citycare.com" are the same host. Dropping it here means the two cannot become two
        // different tenant rows, and that a browser sending the fully-qualified form still
        // resolves.
        host = host.TrimEnd('.');

        return host.Length == 0 ? null : host;
    }

    /// <summary>
    /// The <see cref="DomainName"/> values that could identify a pharmacy for a given browser
    /// address, most specific first.
    ///
    /// <para><b>Why a pharmacy can be named two ways.</b> A pharmacy either sits under the
    /// platform's own address as a prefix — <c>popular-pharmacy.pms.example.com</c>, where
    /// <see cref="DomainName"/> is just <c>popular-pharmacy</c> — or it brings its own address
    /// entirely, <c>citycare.com</c>, where <see cref="DomainName"/> is the whole host. Both
    /// are useful and neither is wrong, so both are allowed in the one column.</para>
    ///
    /// <para>What keeps that from being ambiguous is the order below. An exact host match wins,
    /// so a pharmacy that owns its address is found by it; only if nothing owns the whole host
    /// is the leading label tried against <paramref name="baseDomain"/>. Without a stated
    /// order, a column holding both kinds of value would be exactly the mess this replaces —
    /// two plausible readings of the same string and no rule saying which applies.</para>
    ///
    /// <para>A pharmacy may not register a <see cref="DomainName"/> ending in the base domain;
    /// that would shadow a prefix-based one. The create validator enforces it.</para>
    /// </summary>
    /// <param name="hostOrDomain">A browser host, or a value typed into a login form.</param>
    /// <param name="baseDomain">The platform's own address, or null to skip prefix matching.</param>
    public static IReadOnlyList<string> ResolutionCandidates(
        string? hostOrDomain, string? baseDomain)
    {
        var host = NormalizeDomain(hostOrDomain);

        if (host is null)
        {
            return Array.Empty<string>();
        }

        var normalizedBase = NormalizeDomain(baseDomain);

        // The base domain on its own is the platform's front door, never a pharmacy.
        if (normalizedBase is not null && host == normalizedBase)
        {
            return Array.Empty<string>();
        }

        if (normalizedBase is null)
        {
            return new[] { host };
        }

        var suffix = "." + normalizedBase;

        if (!host.EndsWith(suffix, StringComparison.Ordinal))
        {
            // Some other address entirely: it can only be a pharmacy that owns it outright.
            return new[] { host };
        }

        var label = host[..^suffix.Length];

        // Only the leftmost label names a pharmacy. Anything deeper is a mistake, and treating
        // "a.b.pms.example.com" as a pharmacy called "a.b" turns a typo into a login attempt.
        if (label.Length == 0 || label.Contains('.'))
        {
            return new[] { host };
        }

        return new[] { host, label };
    }
}
