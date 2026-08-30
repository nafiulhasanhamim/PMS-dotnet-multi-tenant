using PMS.SharedKernel.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PMS.Persistence.Interceptors;

/// <summary>
/// EF Core interceptor that automatically populates audit fields
/// (CreatedOnUtc, CreatedBy, ModifiedOnUtc, ModifiedBy) on entities implementing IAuditable.
/// </summary>
public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public AuditableEntityInterceptor(
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditableEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditableEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditableEntities(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var utcNow = _dateTime.UtcNow;
        var currentUser = _currentUserService.UserId ?? _currentUserService.UserName ?? "System";

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    SetCreatedAuditFields(entry, utcNow, currentUser);
                    break;

                case EntityState.Modified:
                    SetModifiedAuditFields(entry, utcNow, currentUser);
                    break;
            }
        }

        // Handle soft delete
        foreach (var entry in context.ChangeTracker.Entries<ISoftDelete>())
        {
            if (entry.State == EntityState.Deleted)
            {
                // Convert hard delete to soft delete
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedOnUtc = utcNow;
                entry.Entity.DeletedBy = currentUser;

                // Also update modified fields if the entity is auditable
                if (entry.Entity is IAuditable auditable)
                {
                    auditable.ModifiedOnUtc = utcNow;
                    auditable.ModifiedBy = currentUser;
                }
            }
        }
    }

    private static void SetCreatedAuditFields(EntityEntry<IAuditable> entry, DateTime utcNow, string user)
    {
        entry.Entity.CreatedOnUtc = utcNow;
        entry.Entity.CreatedBy = user;
    }

    private static void SetModifiedAuditFields(EntityEntry<IAuditable> entry, DateTime utcNow, string user)
    {
        entry.Entity.ModifiedOnUtc = utcNow;
        entry.Entity.ModifiedBy = user;

        // Ensure we don't update created fields on modification
        entry.Property(e => e.CreatedOnUtc).IsModified = false;
        entry.Property(e => e.CreatedBy).IsModified = false;
    }
}
