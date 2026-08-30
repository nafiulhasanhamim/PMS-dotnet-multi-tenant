using PMS.Application;
using PMS.Infrastructure;
using PMS.Infrastructure.Logging;
using PMS.Persistence;
using PMS.WebApi.Extensions;
using PMS.WebApi.Middleware;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

// ════════════════════════════════════════════════════════════════════
// Configure Serilog Bootstrap Logger (for startup errors)
// Skip in test environment to avoid frozen logger issues
// ════════════════════════════════════════════════════════════════════
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
if (!string.Equals(environment, "Testing", StringComparison.OrdinalIgnoreCase))
{
    SerilogConfiguration.CreateBootstrapLogger();
}

try
{
    Log.Information("Starting PMS Web API...");

    var builder = WebApplication.CreateBuilder(args);

    // ════════════════════════════════════════════════════════════════════
    // Configure Serilog
    // ════════════════════════════════════════════════════════════════════
    builder.Host.UseSerilogLogging();

    // ════════════════════════════════════════════════════════════════════
    // Configure Services
    // ════════════════════════════════════════════════════════════════════

    // Add Application Layer services (MediatR, Validators, Mapster, Behaviors)
    builder.Services.AddApplicationServices();

    // Add Persistence Layer services (DbContext, Repositories, Unit of Work)
    builder.Services.AddPersistence(builder.Configuration);

    // Add Infrastructure Layer services (Caching, Email, Storage, HTTP Clients)
    builder.Services.AddInfrastructure(builder.Configuration);

    // Add API controllers
    builder.Services.AddControllers();

    // Add API explorer for Swagger
    builder.Services.AddEndpointsApiExplorer();

    // Add Swagger documentation
    builder.Services.AddSwaggerDocumentation();

    // Add CORS policies
    builder.Services.AddCorsPolicies(builder.Configuration);

    // Add health checks
    builder.Services.AddHealthCheckServices(builder.Configuration);

    // Add ProblemDetails
    builder.Services.AddProblemDetailsConfiguration();

    // Add Aspire ServiceDefaults (OpenTelemetry, Health Checks, Service Discovery, Resilience)
    builder.AddServiceDefaults();

    // ════════════════════════════════════════════════════════════════════
    // Configure HTTP Request Pipeline
    // ════════════════════════════════════════════════════════════════════

    var app = builder.Build();

    // Serilog request logging (adds structured HTTP request info)
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
    });

    // Global exception handler (must be first)
    app.UseGlobalExceptionHandler();

    // Swagger (development only by default, can be configured)
    if (app.Environment.IsDevelopment())
    {
        app.UseSwaggerDocumentation();
    }

    // HTTPS redirection
    app.UseHttpsRedirection();

    // CORS
    app.UseCors(app.Environment.IsDevelopment() ? "Development" : "Production");

    // Authentication & Authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Health checks endpoint
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("db"),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false // Just confirms the app is running
    });

    // Map controllers
    app.MapControllers();

    Log.Information("PMS Web API started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make the implicit Program class public so test projects can access it
public partial class Program { }
