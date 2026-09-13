using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace PMS.Infrastructure.Logging;

/// <summary>
/// Serilog configuration for structured logging.
/// </summary>
public static class SerilogConfiguration
{
    /// <summary>
    /// Creates a bootstrap logger for startup logging before DI is available.
    /// </summary>
    public static void CreateBootstrapLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                path: "Logs/bootstrap-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateBootstrapLogger();
    }

    /// <summary>
    /// Configures Serilog with full configuration from appsettings and enrichers.
    /// </summary>
    public static IHostBuilder UseSerilogLogging(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                // Serilog defaults its global minimum to Information, and there is no
                // Serilog section in appsettings to say otherwise. That gate sits in front
                // of every sink, so the Debug console sink below could never fire and the
                // Debug lines in the handlers went nowhere at all. Opening the gate in
                // Development is what makes that sink mean what it says; the file sinks keep
                // their own Information and Warning floors, so nothing extra reaches disk.
                .MinimumLevel.Is(
                    context.HostingEnvironment.IsDevelopment()
                        ? LogEventLevel.Debug
                        : LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProperty("Application", "PMS")
                .ConfigureDefaultSinks(context.HostingEnvironment);
        });
    }

    /// <summary>
    /// Configures default log sinks based on environment.
    /// </summary>
    private static LoggerConfiguration ConfigureDefaultSinks(
        this LoggerConfiguration configuration,
        IHostEnvironment environment)
    {
        var logPath = "Logs";

        // Console sink - always enabled
        configuration.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}      {Message:lj}{NewLine}{Exception}",
            restrictedToMinimumLevel: environment.IsDevelopment()
                ? LogEventLevel.Debug
                : LogEventLevel.Information);

        // File sink - rolling daily logs
        configuration.WriteTo.File(
            path: Path.Combine(logPath, "pms-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
            restrictedToMinimumLevel: LogEventLevel.Information);

        // JSON file sink - for structured log analysis (Warning and above)
        configuration.WriteTo.File(
            formatter: new CompactJsonFormatter(),
            path: Path.Combine(logPath, "pms-json-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 15,
            restrictedToMinimumLevel: LogEventLevel.Warning);

        return configuration;
    }
}
