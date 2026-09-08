using PMS.Application.Interfaces;
using PMS.Persistence.Contexts;
using DomainTenantStatus = PMS.Domain.Enums.TenantStatus;
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
        var status = await _context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => (DomainTenantStatus?)t.Status)
            .FirstOrDefaultAsync(cancellationToken);

        return status switch
        {
            null => TenantStatus.NotFound,
            DomainTenantStatus.Suspended => TenantStatus.Inactive,
            _ => TenantStatus.Ok,
        };
    }
}
