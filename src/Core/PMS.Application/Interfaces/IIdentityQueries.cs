using PMS.Domain.Entities;

namespace PMS.Application.Interfaces;

/// <summary>
/// The handful of reads that must bypass the tenant query filters.
///
/// Gathered behind one interface on purpose. Every implementation calls
/// <c>IgnoreQueryFilters()</c>, and keeping them here means the sanctioned exceptions live in
/// exactly one file that can be reviewed as a whole — rather than scattered across handlers
/// where a new one could be added unnoticed. See docs/01--users-and-authentication.md.
/// </summary>
public interface IIdentityQueries
{
    /// <summary>Exception 2 — identity is global. Email is matched case-insensitively.</summary>
    Task<User?> FindUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Exception 1 — no tenant context exists yet; this call establishes it.</summary>
    Task<Tenant?> FindTenantByDomainAsync(string domainName, CancellationToken cancellationToken = default);

    Task<Tenant?> FindTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Exception 3 — the membership check that runs as the context is established.</summary>
    Task<UserTenantMembership?> FindMembershipAsync(
        Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Exception 3 — a platform membership has no tenant to be filtered by.</summary>
    Task<UserTenantMembership?> FindPlatformMembershipAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exception 4 — a named pharmacy's staff, read by a platform operator who is inside no
    /// pharmacy at all. Users come back attached.
    /// </summary>
    Task<IReadOnlyList<UserTenantMembership>> ListMembershipsForTenantAsync(
        Guid tenantId, CancellationToken cancellationToken = default);
}
