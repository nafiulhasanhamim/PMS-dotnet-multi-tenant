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
    private readonly ITenantContext? _tenantContext;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// For derived contexts. EF Core requires a context to receive options typed to its own
    /// class, so a subclass cannot reuse the constructors above.
    /// </summary>
    protected ApplicationDbContext(DbContextOptions options, ITenantContext? tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// The tenant every query in this context is confined to.
    ///
    /// Deliberately a public property on the context rather than a captured value: EF Core
    /// turns a query filter that reads a context member into a query *parameter*, re-read on
    /// every execution. Capturing the id in a local, or injecting ITenantContext into an
    /// IEntityTypeConfiguration and reading it there, would instead bake whichever tenant
    /// happened to build the model into the cached model — and every later request, for every
    /// other pharmacy, would silently be filtered to that first tenant.
    ///
    /// Guid.Empty when no tenant is resolved, which matches no rows. See ITenantContext.
    /// </summary>
    public Guid CurrentTenantId => _tenantContext?.TenantId ?? Guid.Empty;

    // PMS entities go here as they are built, e.g.:
    //     public DbSet<Medicine> Medicines => Set<Medicine>();

    /// <summary>
    /// Gets the Tenants DbSet. Not itself tenant-scoped: this is the tenant list, managed
    /// by a platform administrator.
    /// </summary>
    public DbSet<Tenant> Tenants => Set<Tenant>();

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

        // Apply the soft-delete and tenant global query filters
        ApplyQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Applies global query filters for soft delete and tenant isolation.
    ///
    /// Both conditions are composed into a *single* filter per entity, because
    /// HasQueryFilter replaces any filter already set rather than adding to it. Applying
    /// them in two passes would leave whichever ran last in force — and dropping the
    /// soft-delete half would resurrect deleted rows, while dropping the tenant half would
    /// expose every pharmacy's data to every other one.
    /// </summary>
    private void ApplyQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            var softDelete = typeof(ISoftDelete).IsAssignableFrom(clrType);
            var tenantOwned = typeof(ITenantEntity).IsAssignableFrom(clrType);

            var name = (softDelete, tenantOwned) switch
            {
                (true, true) => nameof(ApplyTenantAndSoftDeleteFilter),
                (true, false) => nameof(ApplySoftDeleteFilter),
                (false, true) => nameof(ApplyTenantFilter),
                _ => null,
            };
            if (name is null)
            {
                continue;
            }

            typeof(ApplicationDbContext)
                .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(clrType)
                .Invoke(this, [modelBuilder]);
        }
    }

    private void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDelete
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }

    private void ApplyTenantAndSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDelete, ITenantEntity
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => !e.IsDeleted && e.TenantId == CurrentTenantId);
    }
}
