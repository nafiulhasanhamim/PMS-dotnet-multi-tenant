using System.Reflection;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Entities.Reporting;
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

    #region Shared Entities (also in ApplicationDbContext)

    /// <summary>
    /// Gets the Customers DbSet (read-only).
    /// </summary>
    public DbSet<Customer> Customers => Set<Customer>();

    /// <summary>
    /// Gets the Products DbSet (read-only).
    /// </summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>
    /// Gets the Orders DbSet (read-only).
    /// </summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <summary>
    /// Gets the OrderItems DbSet (read-only).
    /// </summary>
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    #endregion

    #region Reporting-Only Entities (NOT in ApplicationDbContext)

    /// <summary>
    /// Gets the DailySalesSummary DbSet for daily sales analytics.
    /// </summary>
    public DbSet<DailySalesSummary> DailySalesSummaries => Set<DailySalesSummary>();

    /// <summary>
    /// Gets the MonthlySalesSummary DbSet for monthly trend analysis.
    /// </summary>
    public DbSet<MonthlySalesSummary> MonthlySalesSummaries => Set<MonthlySalesSummary>();

    /// <summary>
    /// Gets the ProductSalesAggregate DbSet for product performance analytics.
    /// </summary>
    public DbSet<ProductSalesAggregate> ProductSalesAggregates => Set<ProductSalesAggregate>();

    /// <summary>
    /// Gets the CustomerLifetimeValue DbSet for customer analytics and segmentation.
    /// </summary>
    public DbSet<CustomerLifetimeValue> CustomerLifetimeValues => Set<CustomerLifetimeValue>();

    #endregion

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
