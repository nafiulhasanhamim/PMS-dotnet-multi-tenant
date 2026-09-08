using PMS.SharedKernel.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PMS.Persistence.Interceptors;

/// <summary>
/// Stamps the current tenant onto new rows, and refuses to move an existing row between
/// tenants.
///
/// Handlers therefore never set TenantId themselves. That is the point: a handler that
/// forgets would create an orphan row belonging to no pharmacy, and one that sets it from
/// user input would let a caller write into someone else's data.
/// </summary>
public sealed class TenantEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentTenantService _tenantContext;

    public TenantEntityInterceptor(ICurrentTenantService tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ITenantEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    StampOnInsert(entry);
                    break;

                case EntityState.Modified:
                    RefuseTenantChange(entry);
                    break;
            }
        }
    }

    private void StampOnInsert(EntityEntry<ITenantEntity> entry)
    {
        var current = _tenantContext.TenantId;

        // Writing without a resolved tenant would produce a row no pharmacy owns and no
        // query filter can ever return. Fail loudly at the point of the mistake instead.
        if (current == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Cannot save {entry.Entity.GetType().Name}: no tenant is resolved for this " +
                "request. Tenant-owned data can only be written in the context of a signed-in " +
                "user whose token carries a tenant claim.");
        }

        entry.Property(nameof(ITenantEntity.TenantId)).CurrentValue = current;
    }

    private static void RefuseTenantChange(EntityEntry<ITenantEntity> entry)
    {
        var tenant = entry.Property(nameof(ITenantEntity.TenantId));

        if (!Equals(tenant.OriginalValue, tenant.CurrentValue))
        {
            throw new InvalidOperationException(
                $"Cannot move {entry.Entity.GetType().Name} between tenants. A row belongs to " +
                "the pharmacy that created it for its whole life.");
        }
    }
}
