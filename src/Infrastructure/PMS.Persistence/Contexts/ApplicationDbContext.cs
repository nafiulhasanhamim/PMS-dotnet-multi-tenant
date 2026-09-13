using System.Reflection;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Entities.Catalog;
using PMS.Persistence.Converters;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;
using Microsoft.Data.SqlClient;
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

    /// <summary>
    /// Physical stock: one row per delivery, each with its own expiry and cost. Tenant-scoped
    /// by convention.
    /// </summary>
    public DbSet<Batch> Batches => Set<Batch>();

    /// <summary>
    /// Every non-sale change to a batch quantity, with its reason. Tenant-scoped by
    /// convention. Written only alongside the quantity change it explains — see
    /// <c>Batch.Adjust</c>.
    /// </summary>
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();

    /// <summary>
    /// Completed sales. Tenant-scoped by convention, which is what makes a sale created in one
    /// pharmacy invisible in another even by direct id.
    /// </summary>
    public DbSet<Sale> Sales => Set<Sale>();

    /// <summary>
    /// Sale lines: one per batch touched, so a FEFO split across two batches is two rows for
    /// one thing the customer bought. Tenant-scoped by convention.
    /// </summary>
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();

    /// <summary>
    /// Goods returned against a sale line. Tenant-scoped by convention. Written only alongside
    /// the stock adjustment that puts the units back — see CreateSalesReturnCommandHandler.
    /// </summary>
    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();

    // ── Suppliers and purchases (Module 4) ───────────────────────────────────────────────

    /// <summary>
    /// Who the pharmacy buys from. Tenant-scoped by convention.
    ///
    /// <para>Carries no balance column. What is owed is computed from purchases, returns and
    /// payments on every read — see <c>ISupplierBalanceQueries</c>, which is the only place that
    /// arithmetic exists.</para>
    /// </summary>
    public DbSet<Supplier> Suppliers => Set<Supplier>();

    /// <summary>One delivery against one bill. Tenant-scoped by convention.</summary>
    public DbSet<Purchase> Purchases => Set<Purchase>();

    /// <summary>
    /// One product out of one batch on a purchase. Its quantity and cost deliberately duplicate
    /// the batch's: the batch moves as stock sells, this does not.
    /// </summary>
    public DbSet<PurchaseLine> PurchaseLines => Set<PurchaseLine>();

    /// <summary>Money going out, against a bill or against the account.</summary>
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();

    /// <summary>
    /// Goods going back to a supplier. Written only alongside the stock adjustment that removes
    /// the units — see CreatePurchaseReturnCommandHandler.
    /// </summary>
    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();

    // Nor is there one for PurchaseSequences, for exactly the same reasons as InvoiceSequences
    // below. See PurchaseNumberGenerator.

    // Note there is no DbSet for InvoiceSequences. It is a counter table with no business data,
    // read and written only by InvoiceNumberGenerator through one atomic statement; mapping it
    // would give it a tenant query filter that the generator would then have to bypass. See
    // that class for the reasoning.

    // ── Settings (Module 10) ──────────────────────────────────────────────────

    /// <summary>
    /// One configurable value per pharmacy, keyed by name. Tenant-scoped by convention, which is
    /// what makes two pharmacies' discount caps independent without a call site passing an id.
    ///
    /// <para>Read through <c>ISettingsService</c>, never directly by a handler: values are stored
    /// as text and the casting lives in exactly one place. See <c>AppSetting</c>.</para>
    /// </summary>
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    // ── Salary management (Module 9) ─────────────────────────────────────────────────────

    /// <summary>
    /// What one member of staff is paid, and on what terms. Tenant-scoped by convention.
    ///
    /// <para>Deliberately not columns on <see cref="Users"/>: not every user draws a salary, a
    /// user row is read on every request while this is read a few times a month, and keeping
    /// them apart means a bug in authentication cannot expose what people earn.</para>
    /// </summary>
    public DbSet<EmployeeSalaryProfile> EmployeeSalaryProfiles => Set<EmployeeSalaryProfile>();

    /// <summary>
    /// One employee's salary for one month, once generated. Unique per profile and period.
    ///
    /// <para>Its <c>BaseSalary</c> duplicates the profile's on purpose — the profile says what
    /// somebody earns now, this says what they were paid in a month that has already
    /// happened.</para>
    /// </summary>
    public DbSet<SalaryEntry> SalaryEntries => Set<SalaryEntry>();

    /// <summary>
    /// Cash handed over mid-month, to come out of a later salary.
    ///
    /// <para>An expense on the day it was given, not on the day it is deducted — see
    /// <c>IOperatingExpenses</c>, which sums both this and paid salary entries and would
    /// otherwise lose every advance entirely.</para>
    /// </summary>
    public DbSet<SalaryAdvance> SalaryAdvances => Set<SalaryAdvance>();

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
    /// Makes every date-time property UTC on both sides of the database boundary.
    ///
    /// <para>Done by convention rather than per property, for the same reason the query
    /// filters are: there is no version of this that is safe to opt into one entity at a
    /// time. A single column left Unspecified serialises without a <c>Z</c>, and the client
    /// that reads it cannot tell it apart from the ones that have one. See
    /// <see cref="UtcDateTimeConverter"/> for what that cost before this existed.</para>
    ///
    /// <para>This governs storage and transport only. Showing a Dhaka pharmacist a UTC
    /// timestamp is a presentation problem, solved in the web layer, and it depends on the
    /// value arriving unambiguous — which is what this guarantees.</para>
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
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

    /// <summary>
    /// Saves, translating a unique-constraint violation into
    /// <see cref="DuplicateKeyException"/>.
    ///
    /// <para>Overridden at the context rather than in the unit of work because it is not the
    /// only thing that saves: Ardalis's <c>RepositoryBase.AddAsync</c> calls
    /// <c>SaveChangesAsync</c> itself, so a translation in the unit of work would miss every
    /// insert that went through a repository. This is the one place all of them pass
    /// through.</para>
    ///
    /// <para>Only 2601 and 2627 are translated. Everything else — a foreign key that does not
    /// resolve, a CHECK constraint refusing negative stock — is a bug or a bypassed
    /// validation, and quietly reshaping those into something a handler might swallow is how
    /// a real defect gets a friendly message and survives to production.</para>
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex, out var constraintName))
        {
            throw new DuplicateKeyException(constraintName, ex);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception, out string? constraintName)
    {
        constraintName = null;

        // Typed against the SQL Server driver's own numbers: 2627 is a unique constraint or
        // primary key, 2601 a unique index. Matching on the message text instead would break
        // on a server with a non-English collation of messages.
        if (exception.InnerException is not SqlException { Number: 2601 or 2627 } sql)
        {
            return false;
        }

        // The driver puts the index name in the message in quotes. Best-effort: a null name
        // still produces the right exception type, and the handler's own check has almost
        // always identified the clash already.
        var message = sql.Message;
        var open = message.IndexOf('\'');
        var close = open >= 0 ? message.IndexOf('\'', open + 1) : -1;

        if (open >= 0 && close > open + 1)
        {
            constraintName = message[(open + 1)..close];
        }

        return true;
    }
}
