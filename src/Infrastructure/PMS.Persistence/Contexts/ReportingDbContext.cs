using System.Reflection;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace PMS.Persistence.Contexts;

/// <summary>
/// Read-optimized database context for reporting and analytics.
/// Typically connects to a read replica or uses a separate connection for read-heavy operations.
/// </summary>
/// <remarks>
/// This context:
/// - Uses AsNoTracking by default for better read performance
/// - Does NOT dispatch domain events (read-only context)
/// - Does NOT update audit fields (read-only context)
/// - Can connect to a read replica database
/// - Contains reporting-specific entities not present in ApplicationDbContext
/// </remarks>
public class ReportingDbContext : DbContext, IReportingDbContext
{
    public ReportingDbContext(DbContextOptions<ReportingDbContext> options)
        : base(options)
    {
        // Disable change tracking by default for read-heavy operations
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        ChangeTracker.AutoDetectChangesEnabled = false;
    }

    // Read-side DbSets go here as the PMS reports module is built, e.g.:
    //     public DbSet<Sale> Sales => Set<Sale>();                       // shared with ApplicationDbContext
    //     public DbSet<DailySalesSummary> DailySales => Set<DailySalesSummary>();   // reporting-only

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ignore non-entity types that EF Core might try to map
        modelBuilder.Ignore<DomainEvent>();

        // Apply the same configurations as ApplicationDbContext
        // This ensures consistent mapping across both contexts
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Apply soft delete query filters
        ApplySoftDeleteFilters(modelBuilder);
    }

    private static void ApplySoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(ReportingDbContext)
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
