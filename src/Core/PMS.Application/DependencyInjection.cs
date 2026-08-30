using System.Reflection;
using PMS.Application.Common.Mappings;
using PMS.SharedKernel.Behaviors;
using PMS.SharedKernel.DependencyInjection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace PMS.Application;

/// <summary>
/// Dependency injection configuration for the Application layer.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Application layer services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register MediatR handlers
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
        });

        // Register MediatR pipeline behaviors in execution order (outermost to innermost):
        // 1. UnhandledExceptionBehavior - Catches all exceptions (outermost)
        // 2. LoggingBehavior - Logs request start/end with timing
        // 3. PerformanceBehavior - Monitors slow requests (>500ms)
        // 4. ValidationBehavior - Validates request before handler
        // 5. AccessLogBehavior - Logs access after successful handler execution (innermost)
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AccessLogBehavior<,>));

        // Register FluentValidation validators
        services.AddValidatorsFromAssembly(assembly);

        // Register Mapster configuration
        services.RegisterMapsterConfiguration();

        // Register services by lifetime marker interfaces
        services.RegisterAllServiceLifetimes(assembly);

        return services;
    }
}
