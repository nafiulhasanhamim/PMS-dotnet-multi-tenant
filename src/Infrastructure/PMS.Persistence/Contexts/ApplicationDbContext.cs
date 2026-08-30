using System.Reflection;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace PMS.Persistence.Contexts;

/// <summary>
/// Main application database context.
/// Implements IApplicationDbContext marker interface for type-safe DI.
/// </summary>
public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // PMS entities go here as they are built, e.g.:
    //     public DbSet<Medicine> Medicines => Set<Medicine>();

    /// <summary>
    /// Gets the AccessLogs DbSet for audit trail.
    /// </summary>
    public DbSet<AccessLog> AccessLogs => Set<AccessLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ignore non-entity types that EF Core might try to map
        modelBuilder.Ignore<DomainEvent>();

        // Apply all entity configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Apply soft delete query filters to all ISoftDelete entities
        ApplySoftDeleteFilters(modelBuilder);
    }

    /// <summary>
    /// Applies global query filters for soft delete to all entities implementing ISoftDelete.
    /// </summary>
    private static void ApplySoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(ApplySoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType);

                method.Invoke(null, [modelBuilder]);
            }
        }
    }

    private static void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDelete
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }
}
