using PMS.Application.Interfaces;
using PMS.Persistence.Contexts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

namespace PMS.PerformanceTests.Common;

/// <summary>
/// Custom WebApplicationFactory for load testing.
/// Configures test-specific services and uses in-memory database.
/// </summary>
public class LoadTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName;

    // Static constructor ensures environment is set before any instance is created
    static LoadTestWebApplicationFactory()
    {
        // Set testing environment BEFORE any host building occurs
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        // Reset Serilog to a simple logger for tests to avoid frozen logger issues
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Console()
            .CreateLogger();
    }

    public LoadTestWebApplicationFactory()
    {
        // Use unique database name for test isolation
        _databaseName = $"PMS_LoadTests_{Guid.NewGuid():N}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Clear connection strings to prevent health checks from being added
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = null,
                ["ConnectionStrings:Redis"] = null
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Remove existing DbContext factory if any
            var factoryDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IDbContextFactory<ApplicationDbContext>));

            if (factoryDescriptor != null)
            {
                services.Remove(factoryDescriptor);
            }

            // Remove external health checks (SQL Server, Redis)
            services.RemoveAll<IHealthCheck>();

            // Add ApplicationDbContext using in-memory database for fast testing
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            // Re-register IApplicationDbContext interface
            services.AddScoped<IApplicationDbContext>(sp =>
                sp.GetRequiredService<ApplicationDbContext>());
        });

        builder.UseEnvironment("Testing");
    }

    /// <summary>
    /// Ensures database is created after host is built.
    /// </summary>
    public void EnsureDatabaseCreated()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
    }
}
