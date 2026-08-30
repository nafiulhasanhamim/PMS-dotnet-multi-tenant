using PMS.Application.Interfaces;
using PMS.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PMS.Infrastructure.Dapper;

/// <summary>
/// Extension methods for registering Dapper services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Dapper services to the service collection.
    /// Registers IDapperService for the default connection and typed services for specific databases.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDapper(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var defaultConnection = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection string not found.");

        var reportingConnection = configuration.GetConnectionString("ReportingConnection")
            ?? defaultConnection; // Fall back to default if not specified

        // Register default IDapperService (uses DefaultConnection)
        services.AddScoped<IDapperService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DapperService>>();
            return new DapperService(defaultConnection, logger);
        });

        // Register typed IDapperService<IApplicationDbContext> (same as default)
        services.AddScoped<IDapperService<IApplicationDbContext>>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DapperService<IApplicationDbContext>>>();
            return new DapperService<IApplicationDbContext>(defaultConnection, logger);
        });

        // Register typed IDapperService<IReportingDbContext> (uses ReportingConnection)
        services.AddScoped<IDapperService<IReportingDbContext>>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DapperService<IReportingDbContext>>>();
            return new DapperService<IReportingDbContext>(reportingConnection, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds a custom Dapper service for a specific database marker.
    /// </summary>
    /// <typeparam name="TDb">The database marker interface.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionStringName">The connection string name from configuration.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDapper<TDb>(
        this IServiceCollection services,
        string connectionStringName,
        IConfiguration configuration)
        where TDb : class
    {
        var connectionString = configuration.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException($"{connectionStringName} connection string not found.");

        services.AddScoped<IDapperService<TDb>>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DapperService<TDb>>>();
            return new DapperService<TDb>(connectionString, logger);
        });

        return services;
    }
}
