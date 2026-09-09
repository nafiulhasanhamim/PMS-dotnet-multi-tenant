using System.Reflection;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Entities.Catalog;
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
    private readonly ICurrentTenantService? _tenantContext;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentTenantService tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// For derived contexts. EF Core requires a context to receive options typed to its own
    /// class, so a subclass cannot reuse the constructors above.
    /// </summary>
    protected ApplicationDbContext(DbContextOptions options, ICurrentTenantService? tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// The tenant every query in this context is confined to.
    ///
    /// Deliberately a public property on the context rather than a captured value: EF Core
    /// turns a query filter that reads a context member into a query *parameter*, re-read on
    /// every execution. Capturing the id in a local, or injecting ICurrentTenantService into an
    /// IEntityTypeConfiguration and reading it there, would instead bake whichever tenant
    /// happened to build the model into the cached model — and every later request, for every
    /// other pharmacy, would silently be filtered to that first tenant.
    ///
    /// Guid.Empty when no tenant is resolved, which matches no rows. See ICurrentTenantService.
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
    /// Gets the Users DbSet. A global identity table, deliberately not tenant-scoped — a
    /// login has to find a user before any pharmacy is known.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Gets the memberships. Filtered to the current pharmacy, but by its own rule rather
    /// than the generic one — see ApplyQueryFilters.
    /// </summary>
    public DbSet<UserTenantMembership> UserTenantMemberships => Set<UserTenantMembership>();

    /// <summary>
    /// Gets the AccessLogs DbSet for audit trail.
    /// </summary>
    public DbSet<AccessLog> AccessLogs => Set<AccessLog>();

    /// <summary>
    /// This pharmacy's own catalogue of what it sells - medicines and non-medicines alike.
    /// Tenant-scoped: filtered and stamped by convention, because Product implements
    /// ITenantEntity.
    /// </summary>
    public DbSet<Product> Products => Set<Product>();

    // ── Medicine reference catalog ───────────────────────────────────────────────────────
    //
    // Platform-level and shared: none of these implement ITenantEntity, so none is filtered
    // by tenant and none is stamped by the interceptor. A query here returns the same rows
    // for every pharmacy, and for no pharmacy at all. Tenant users only ever read them; the
    // importer in tools/PMS.DataImport is what writes them.

    public DbSet<CatalogManufacturer> CatalogManufacturers => Set<CatalogManufacturer>();

    public DbSet<CatalogDrugClass> CatalogDrugClasses => Set<CatalogDrugClass>();

    public DbSet<CatalogDosageForm> CatalogDosageForms => Set<CatalogDosageForm>();

    public DbSet<CatalogGeneric> CatalogGenerics => Set<CatalogGeneric>();

    public DbSet<CatalogMedicine> CatalogMedicines => Set<CatalogMedicine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ignore non-entity types that EF Core might try to map
        modelBuilder.Ignore<DomainEvent>();

        // Apply all entity configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Apply the soft-delete and tenant global query filters
        ApplyQueryFilters(modelBuilder);

        // Memberships are filtered separately — see the method for why.
        ApplyMembershipFilter(modelBuilder);
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

    /// <summary>
    /// Confines memberships to the current pharmacy.
    ///
    /// Kept out of the generic loop above because UserTenantMembership is not an
    /// ITenantEntity and could not be: its TenantId is *nullable*, since a platform
    /// operator's membership has no pharmacy at all. The generic filter compares a
    /// non-nullable Guid, so it would neither compile against this shape nor mean the right
    /// thing.
    ///
    /// Note what falls out for free: platform rows have TenantId = null, and null never
    /// equals a tenant id, so they are excluded without any special case. Inside a pharmacy
    /// you see that pharmacy's memberships and nothing else — not another pharmacy's, and
    /// not the platform operators'.
    ///
    /// Login and platform administration read this table before any tenant exists, and do so
    /// through IgnoreQueryFilters(). Those are named exhaustively in
    /// docs/01-tenant-foundation-and-auth.md.
    /// </summary>
    private void ApplyMembershipFilter(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserTenantMembership>()
            .HasQueryFilter(m => m.TenantId == CurrentTenantId);
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
