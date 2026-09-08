using PMS.Application.Interfaces;
using PMS.Persistence.Contexts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Serilog;

namespace PMS.IntegrationTests.Common;

/// <summary>
/// Custom WebApplicationFactory for integration testing.
/// Configures test-specific services and uses a unique database per test run.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName;

    // Static constructor ensures environment is set before any instance is created
    static CustomWebApplicationFactory()
    {
        // Set testing environment BEFORE any host building occurs
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        // As environment variables, not ConfigureAppConfiguration: startup reads the signing
        // key while Program.cs is still executing, which is before the factory's
        // configuration callbacks are applied. CreateBuilder picks env vars up immediately.
        Environment.SetEnvironmentVariable("Jwt__Issuer", "PMS.Tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "PMS.Tests");
        Environment.SetEnvironmentVariable(
            "Jwt__SigningKey", "integration-tests-only-signing-key-at-least-32-bytes");
        Environment.SetEnvironmentVariable("Jwt__ExpiryMinutes", "60");

        // Reset Serilog to a simple logger for tests to avoid frozen logger issues
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Console()
            .CreateLogger();
    }

    public CustomWebApplicationFactory()
    {
        // Use unique database name for test isolation
        _databaseName = $"PMS_IntegrationTests_{Guid.NewGuid():N}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Clear connection strings to prevent health checks from being added
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = null,
                ["ConnectionStrings:Redis"] = null,

                // Startup refuses to run without a signing key — deliberately, so no
                // environment can fall back to a predictable one. Supply a test key here.
                ["Jwt:Issuer"] = "PMS.Tests",
                ["Jwt:Audience"] = "PMS.Tests",
                ["Jwt:SigningKey"] = "integration-tests-only-signing-key-at-least-32-bytes",
                ["Jwt:ExpiryMinutes"] = "60"
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
                options.EnableSensitiveDataLogging();
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

    /// <summary>
    /// Creates a new scope and returns the ApplicationDbContext for seeding data.
    /// </summary>
    public ApplicationDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    /// <summary>
    /// Resets the database by clearing all data.
    /// Useful for test isolation.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Clear all data. Add each PMS DbSet here as it is introduced, newest
        // dependants first so foreign keys are satisfied, e.g.:
        //     db.SaleLines.RemoveRange(db.SaleLines.IgnoreQueryFilters());
        //     db.Sales.RemoveRange(db.Sales.IgnoreQueryFilters());
        db.AccessLogs.RemoveRange(db.AccessLogs);

        await db.SaveChangesAsync();
    }
}
