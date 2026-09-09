using PMS.Application.Interfaces;
using PMS.Persistence.Common;
using PMS.Persistence.Contexts;
using PMS.Persistence.Interceptors;
using PMS.Persistence.Services;
using PMS.Persistence.Repositories;
using PMS.SharedKernel.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PMS.Persistence;

/// <summary>
/// Extension methods for registering persistence services.
/// Supports both single-database and multi-database scenarios.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds persistence services to the service collection.
    /// Configures ApplicationDbContext as the primary database.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        return services.AddPersistence(connectionString);
    }

    /// <summary>
    /// Adds persistence services with a custom connection string.
    /// Configures ApplicationDbContext as the primary database.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The database connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));
        }

        // Register interceptors (shared across contexts that need them)
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<TenantEntityInterceptor>();
        services.AddScoped<ITenantStatusValidator, TenantStatusValidator>();
        services.AddScoped<IIdentityQueries, IdentityQueries>();
        services.AddScoped<IPlatformQueries, PlatformQueries>();
        services.AddScoped<ITenantUserQueries, TenantUserQueries>();

        // Module 2: the product catalogue, and the two-stage catalogue search behind it.
        services.AddScoped<IProductQueries, ProductQueries>();
        services.AddScoped<ICatalogSearchQueries, CatalogSearchQueries>();

        // Module 3: batches, adjustments and the FEFO reads over them.
        services.AddScoped<IStockQueries, StockQueries>();

        services.AddScoped<DomainEventDispatcherInterceptor>();

        // Register ApplicationDbContext (primary database with full write capabilities)
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(
                // Tenant first: a row must be stamped with its owner before anything else
                // reasons about it.
                sp.GetRequiredService<TenantEntityInterceptor>(),
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<DomainEventDispatcherInterceptor>());

            ConfigureSqlServer(options, connectionString, typeof(ApplicationDbContext));
        });

        // Register IApplicationDbContext marker interface
        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        // Register repositories and unit of work
        RegisterRepositories(services);

        return services;
    }

    /// <summary>
    /// Adds a reporting/read-replica database context.
    /// Use this for read-heavy operations and analytics.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="connectionStringName">The connection string name (default: "ReportingConnection").</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddReportingDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "ReportingConnection")
    {
        var connectionString = configuration.GetConnectionString(connectionStringName);

        // If no separate reporting connection, fall back to default
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    $"Neither '{connectionStringName}' nor 'DefaultConnection' connection string found.");
        }

        return services.AddReportingDatabase(connectionString);
    }

    /// <summary>
    /// Adds a reporting/read-replica database context with a custom connection string.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The reporting database connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddReportingDatabase(
        this IServiceCollection services,
        string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));
        }

        // Register ReportingDbContext (read-optimized, no interceptors for writes)
        services.AddDbContext<ReportingDbContext>((sp, options) =>
        {
            // No audit or domain event interceptors - this is read-only
            ConfigureSqlServer(options, connectionString, typeof(ReportingDbContext));
        });

        // Register IReportingDbContext marker interface
        services.AddScoped<IReportingDbContext>(sp =>
            sp.GetRequiredService<ReportingDbContext>());

        // Register reporting-specific read repository
        services.AddScoped(typeof(IReadRepository<,>), typeof(ReportingRepository<>));

        return services;
    }

    /// <summary>
    /// Adds both primary and reporting databases with full multi-database support.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPersistenceWithReporting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddReportingDatabase(configuration);
        return services;
    }

    /// <summary>
    /// Registers all repository and unit of work services.
    /// </summary>
    private static void RegisterRepositories(IServiceCollection services)
    {
        // ════════════════════════════════════════════════════════════════════
        // Multi-database pattern (type-safe, recommended for multiple contexts)
        // Usage: IRepository<Supplier, IApplicationDbContext>
        // ════════════════════════════════════════════════════════════════════

        // Generic open repositories (resolved based on TContext type parameter)
        services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
        services.AddScoped(typeof(IReadRepository<,>), typeof(ReadRepository<,>));

        // Unit of Work for multi-database pattern
        services.AddScoped(typeof(IUnitOfWork<>), typeof(UnitOfWork<>));

        // ════════════════════════════════════════════════════════════════════
        // Single-database pattern (convenience interfaces for simple scenarios)
        // Usage: IRepository<Supplier>
        // ════════════════════════════════════════════════════════════════════

        // These use ApplicationDbContext by default
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped(typeof(IReadRepository<>), typeof(ReadRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register DbContext for single-database Repository<T>
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // ════════════════════════════════════════════════════════════════════
        // Domain Event Dispatcher
        // ════════════════════════════════════════════════════════════════════

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
    }

    /// <summary>
    /// Configures SQL Server options for a DbContext.
    /// </summary>
    private static void ConfigureSqlServer(DbContextOptionsBuilder options, string connectionString, Type contextType)
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(contextType.Assembly.FullName);
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });

        #if DEBUG
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
        #endif
    }
}
