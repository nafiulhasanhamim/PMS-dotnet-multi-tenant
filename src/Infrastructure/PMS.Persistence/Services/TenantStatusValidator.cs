using PMS.Application.Interfaces;
using PMS.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace PMS.Persistence.Services;

/// <inheritdoc />
public sealed class TenantStatusValidator : ITenantStatusValidator
{
    private readonly ApplicationDbContext _context;

    public TenantStatusValidator(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<TenantStatus> CheckAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return TenantStatus.NotFound;
        }

        // Tenants carries the soft-delete filter, so a deleted pharmacy simply is not found
        // — there is no separate check for it here.
        var isActive = await _context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => (bool?)t.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        return isActive switch
        {
            null => TenantStatus.NotFound,
            false => TenantStatus.Inactive,
            true => TenantStatus.Ok,
        };
    }
}
