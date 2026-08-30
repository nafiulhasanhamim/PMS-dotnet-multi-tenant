namespace PMS.WebApi.Extensions;

/// <summary>
/// Extension methods for configuring services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds CORS configuration.
    /// </summary>
    public static IServiceCollection AddCorsPolicies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            // Development policy - allows all
            options.AddPolicy("Development", builder =>
            {
                builder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });

            // Production policy - restricted to configured origins
            options.AddPolicy("Production", builder =>
            {
                if (allowedOrigins.Length > 0)
                {
                    builder
                        .WithOrigins(allowedOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                }
                else
                {
                    // Fallback: deny all cross-origin requests if no origins configured
                    builder.SetIsOriginAllowed(_ => false);
                }
            });

            // Default policy
            options.AddDefaultPolicy(builder =>
            {
                if (allowedOrigins.Length > 0)
                {
                    builder
                        .WithOrigins(allowedOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                }
                else
                {
                    builder.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                }
            });
        });

        return services;
    }

    /// <summary>
    /// Adds health check services.
    /// </summary>
    public static IServiceCollection AddHealthCheckServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var healthChecksBuilder = services.AddHealthChecks();

        // Add database health check if connection string exists
        var dbConnectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(dbConnectionString))
        {
            healthChecksBuilder.AddSqlServer(
                dbConnectionString,
                name: "database",
                tags: ["db", "sql", "sqlserver"]);
        }

        // Add Redis health check if connection string exists
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            healthChecksBuilder.AddRedis(
                redisConnectionString,
                name: "redis",
                tags: ["cache", "redis"]);
        }

        return services;
    }

    /// <summary>
    /// Adds API versioning support.
    /// </summary>
    public static IServiceCollection AddApiVersioningSupport(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
        });

        return services;
    }

    /// <summary>
    /// Adds problem details configuration.
    /// </summary>
    public static IServiceCollection AddProblemDetailsConfiguration(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            };
        });

        return services;
    }
}
