using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace PMS.Persistence.Services;

/// <inheritdoc />
public sealed class PlatformQueries : IPlatformQueries
{
    private readonly ApplicationDbContext _context;

    public PlatformQueries(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TenantDto>> ListTenantsAsync(
        CancellationToken cancellationToken = default) =>
        // Tenants carries only the soft-delete filter, so this returns every live pharmacy
        // whatever its status — a platform operator needs to see suspended ones to restore
        // them.
        await _context.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TenantDto(t.Id, t.Name, t.DomainName, t.Status,
                t.SubscriptionPlan, t.CreatedOnUtc))
            .ToListAsync(cancellationToken);
}

/// <inheritdoc />
public sealed class TenantUserQueries : ITenantUserQueries
{
    private readonly ApplicationDbContext _context;

    public TenantUserQueries(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TenantUserDto>> ListForCurrentTenantAsync(
        CancellationToken cancellationToken = default) =>
        // No WHERE on TenantId anywhere here. The membership query filter supplies it, and
        // platform rows (TenantId = null) never match a real tenant, so they are excluded
        // without being mentioned.
        await _context.UserTenantMemberships
            .AsNoTracking()
            .Include(m => m.User)
            .OrderBy(m => m.User.FullName)
            .Select(m => new TenantUserDto(m.Id, m.UserId, m.User.Email, m.User.FullName,
                m.Role, m.IsActive, m.JoinedAt))
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<TenantUserDto?> SetActiveAsync(
        Guid membershipId, bool isActive, CancellationToken cancellationToken = default)
    {
        // Filtered, so another pharmacy's membership is genuinely absent rather than
        // forbidden — the caller gets a 404 and learns nothing about it.
        var membership = await _context.UserTenantMemberships
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == membershipId, cancellationToken);

        if (membership is null)
        {
            return null;
        }

        if (isActive)
        {
            membership.Activate();
        }
        else
        {
            membership.Deactivate();
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new TenantUserDto(membership.Id, membership.UserId, membership.User.Email,
            membership.User.FullName, membership.Role, membership.IsActive, membership.JoinedAt);
    }
}
