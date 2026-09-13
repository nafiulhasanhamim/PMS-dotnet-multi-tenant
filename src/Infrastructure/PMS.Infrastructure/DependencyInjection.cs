using PMS.Application.Interfaces;
using PMS.Infrastructure.Caching;
using PMS.Infrastructure.Dapper;
using PMS.Infrastructure.Email;
using PMS.Infrastructure.Http;
using PMS.Infrastructure.Security;
using PMS.Infrastructure.Services;
using PMS.Infrastructure.Storage;
using PMS.SharedKernel.Behaviors;
using PMS.SharedKernel.DependencyInjection;
using PMS.SharedKernel.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PMS.Infrastructure;

/// <summary>
/// Extension methods for registering infrastructure services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds infrastructure services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register core services
        services.AddCoreServices();

        // Register caching
        services.AddCaching(configuration);

        // Register email services
        services.AddEmailServices(configuration);

        // Register file storage
        services.AddFileStorage(configuration);

        // Register Dapper services for raw SQL and stored procedures
        services.AddDapper(configuration);

        // Auto-register services using lifetime markers from Infrastructure assembly
        services.RegisterAllServiceLifetimes(typeof(DependencyInjection).Assembly);

        return services;
    }

    /// <summary>
    /// Adds core infrastructure services (DateTime, CurrentUser, AccessLogger, etc.).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // HttpContext accessor is required for CurrentUserService and AccessLoggerService
        services.AddHttpContextAccessor();

        // Register core services explicitly (they use marker interfaces but may need manual registration for clarity)
        services.AddSingleton<IDateTime, DateTimeService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICurrentTenantService, CurrentTenantService>();

        // Authentication building blocks
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        // Register access logging services
        services.AddScoped<IAccessLoggerService, AccessLoggerService>();
        services.AddScoped<IAccessLogger, AccessLoggerService>();

        return services;
    }

    /// <summary>
    /// Adds caching services (Memory or Redis).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConnection = configuration.GetConnectionString("Redis");

        if (!string.IsNullOrEmpty(redisConnection))
        {
            // Use Redis distributed cache
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "PMS:";
            });

            services.AddScoped<ICacheService, RedisCacheService>();
        }
        else
        {
            // Use in-memory cache (default)
            services.AddMemoryCache();
            services.AddScoped<ICacheService, MemoryCacheService>();
        }

        return services;
    }

    /// <summary>
    /// Adds email services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEmailServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.AddTransient<IEmailService, SmtpEmailService>();

        return services;
    }

    /// <summary>
    /// Adds file storage services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFileStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        return services;
    }

    /// <summary>
    /// Adds a named HTTP client with resilience policies (retry, circuit breaker, timeout).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The client name.</param>
    /// <param name="configureOptions">Optional configuration action.</param>
    /// <returns>The IHttpClientBuilder for further configuration.</returns>
    public static IHttpClientBuilder AddResilientHttpClient(
        this IServiceCollection services,
        string name,
        Action<HttpClientOptions>? configureOptions = null)
    {
        var options = new HttpClientOptions { Name = name };
        configureOptions?.Invoke(options);

        return services.AddHttpClient<IHttpClientService, HttpClientService>(name, client =>
            {
                if (!string.IsNullOrEmpty(options.BaseUrl))
                {
                    client.BaseAddress = new Uri(options.BaseUrl);
                }

                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds * 2); // Outer timeout

                foreach (var header in options.DefaultHeaders)
                {
                    client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            })
            .AddPolicyHandler((sp, _) =>
            {
                var logger = sp.GetRequiredService<ILogger<HttpClientService>>();
                return HttpPolicyProvider.GetCombinedPolicy(options, logger);
            });
    }

    /// <summary>
    /// Adds a named HTTP client with configuration from appsettings.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The client name (also used as configuration section name).</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The IHttpClientBuilder for further configuration.</returns>
    public static IHttpClientBuilder AddResilientHttpClient(
        this IServiceCollection services,
        string name,
        IConfiguration configuration)
    {
        var options = new HttpClientOptions { Name = name };
        configuration.GetSection($"HttpClients:{name}").Bind(options);

        return services.AddResilientHttpClient(name, opt =>
        {
            opt.BaseUrl = options.BaseUrl;
            opt.TimeoutSeconds = options.TimeoutSeconds;
            opt.EnableRetry = options.EnableRetry;
            opt.RetryCount = options.RetryCount;
            opt.RetryDelaySeconds = options.RetryDelaySeconds;
            opt.EnableCircuitBreaker = options.EnableCircuitBreaker;
            opt.CircuitBreakerThreshold = options.CircuitBreakerThreshold;
            opt.CircuitBreakerDurationSeconds = options.CircuitBreakerDurationSeconds;
            foreach (var header in options.DefaultHeaders)
            {
                opt.DefaultHeaders[header.Key] = header.Value;
            }
        });
    }
}
