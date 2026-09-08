using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace PMS.Persistence.Services;

/// <summary>
/// The complete set of reads that bypass the tenant query filters.
///
/// Every IgnoreQueryFilters() in the application is in this file. That is the point: they are
/// reviewable as a list rather than scattered through handlers, and anything outside here
/// that needs one is a signal the entity was modelled wrongly.
/// </summary>
public sealed class IdentityQueries : IIdentityQueries
{
    private readonly ApplicationDbContext _context;

    public IdentityQueries(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<User?> FindUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = User.NormalizeEmail(email);

        // EXCEPTION 2: identity is global. Users carries no tenant filter, so this needs no
        // bypass today — but it is stated explicitly so the intent survives if one is ever
        // added by mistake.
        return _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
    }

    /// <inheritdoc />
    public Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    /// <inheritdoc />
    public Task<Tenant?> FindTenantByDomainAsync(
        string domainName, CancellationToken cancellationToken = default)
    {
        var normalized = Tenant.NormalizeDomain(domainName);
        if (normalized is null)
        {
            return Task.FromResult<Tenant?>(null);
        }

        // EXCEPTION 1: no tenant context exists yet — resolving this domain is what creates
        // it. Note IgnoreQueryFilters also drops the soft-delete filter, so a deleted
        // pharmacy is excluded explicitly rather than implicitly.
        return _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.DomainName == normalized && !t.IsDeleted, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Tenant?> FindTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, cancellationToken);

    /// <inheritdoc />
    public Task<UserTenantMembership?> FindMembershipAsync(
        Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
        // EXCEPTION 3: runs while the tenant context is being established, so the membership
        // filter would compare against Guid.Empty and find nothing.
        _context.UserTenantMemberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == userId, cancellationToken);

    /// <inheritdoc />
    public Task<UserTenantMembership?> FindPlatformMembershipAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        // EXCEPTION 3: a platform membership has TenantId = null, which no tenant context can
        // ever match.
        _context.UserTenantMemberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                m => m.UserId == userId
                     && m.TenantId == null
                     && m.Role == UserRole.PlatformAdmin,
                cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserTenantMembership>> ListMembershipsForTenantAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        // EXCEPTION 4: a platform operator holds no tenant, so the membership filter would
        // compare against Guid.Empty and return nothing. The tenant id is supplied explicitly
        // and filtered on here — which is exactly why this belongs in this file and not in a
        // handler: the WHERE clause below is the only thing keeping this read from spanning
        // every pharmacy at once.
        await _context.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(m => m.User)
            .Where(m => m.TenantId == tenantId)
            .OrderBy(m => m.User.FullName)
            .ToListAsync(cancellationToken);
}
