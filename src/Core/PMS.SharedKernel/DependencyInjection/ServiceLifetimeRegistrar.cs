using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace PMS.SharedKernel.DependencyInjection;

/// <summary>
/// Generic service registrar that automatically registers services based on their lifetime marker interfaces.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pattern:</b> Service interfaces inherit from marker interfaces (ITransientService, IScopedService, ISingletonService).
/// The registrar finds all implementations and registers them against their service interfaces.
/// </para>
/// <para>
/// <b>Example:</b>
/// <code>
/// // Interface inherits from marker
/// public interface IEmailService : ITransientService { }
///
/// // Class implements interface
/// public class EmailService : IEmailService { }
///
/// // Registration
/// services.RegisterAllServiceLifetimes(typeof(EmailService).Assembly);
/// // Result: services.AddTransient&lt;IEmailService, EmailService&gt;()
/// </code>
/// </para>
/// </remarks>
public static class ServiceLifetimeRegistrar
{
    /// <summary>
    /// Registers services with the specified lifetime from the given assemblies.
    /// </summary>
    /// <typeparam name="TLifetimeMarker">The lifetime marker interface (ITransientService, IScopedService, ISingletonService).</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceLifetime">The service lifetime to register with.</param>
    /// <param name="assemblies">Assemblies to scan for services.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection RegisterServicesByLifetime<TLifetimeMarker>(
        this IServiceCollection services,
        ServiceLifetime serviceLifetime,
        params Assembly[] assemblies)
        where TLifetimeMarker : class
    {
        foreach (var assembly in assemblies)
        {
            RegisterServicesFromAssembly<TLifetimeMarker>(services, assembly, serviceLifetime);
        }

        return services;
    }

    /// <summary>
    /// Automatically registers all services with their respective lifetimes from the specified assemblies.
    /// Scans for ITransientService, IScopedService, and ISingletonService markers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Assemblies to scan for services.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection RegisterAllServiceLifetimes(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        foreach (var assembly in assemblies)
        {
            // Register all three lifetimes
            RegisterServicesFromAssembly<ITransientService>(services, assembly, ServiceLifetime.Transient);
            RegisterServicesFromAssembly<IScopedService>(services, assembly, ServiceLifetime.Scoped);
            RegisterServicesFromAssembly<ISingletonService>(services, assembly, ServiceLifetime.Singleton);
        }

        return services;
    }

    /// <summary>
    /// Registers transient services from the specified assemblies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Assemblies to scan for transient services.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection RegisterTransientServices(
        this IServiceCollection services,
        params Assembly[] assemblies)
        => services.RegisterServicesByLifetime<ITransientService>(ServiceLifetime.Transient, assemblies);

    /// <summary>
    /// Registers scoped services from the specified assemblies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Assemblies to scan for scoped services.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection RegisterScopedServices(
        this IServiceCollection services,
        params Assembly[] assemblies)
        => services.RegisterServicesByLifetime<IScopedService>(ServiceLifetime.Scoped, assemblies);

    /// <summary>
    /// Registers singleton services from the specified assemblies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Assemblies to scan for singleton services.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection RegisterSingletonServices(
        this IServiceCollection services,
        params Assembly[] assemblies)
        => services.RegisterServicesByLifetime<ISingletonService>(ServiceLifetime.Singleton, assemblies);

    /// <summary>
    /// Core implementation that registers services from a single assembly.
    /// </summary>
    private static void RegisterServicesFromAssembly<TLifetimeMarker>(
        IServiceCollection services,
        Assembly assembly,
        ServiceLifetime serviceLifetime)
        where TLifetimeMarker : class
    {
        var markerType = typeof(TLifetimeMarker);

        // Find all concrete classes that implement the lifetime marker interface
        // Exclude generic type definitions (e.g., Repository<T>) - they must be registered manually
        var serviceTypes = assembly.GetExportedTypes()
            .Where(type => type.IsClass && !type.IsAbstract && !type.IsGenericTypeDefinition)
            .Where(type => type.GetInterfaces().Any(i => markerType.IsAssignableFrom(i)))
            .ToList();

        foreach (var implementationType in serviceTypes)
        {
            // Get all service interfaces that inherit from the marker interface (excluding the marker itself)
            var serviceInterfaces = implementationType.GetInterfaces()
                .Where(i => markerType.IsAssignableFrom(i) && i != markerType)
                .ToList();

            foreach (var serviceInterface in serviceInterfaces)
            {
                // Register the service with the specified lifetime
                var serviceDescriptor = new ServiceDescriptor(serviceInterface, implementationType, serviceLifetime);
                services.Add(serviceDescriptor);
            }

            // If no specific service interfaces found, register the implementation type itself
            if (serviceInterfaces.Count == 0 && markerType.IsAssignableFrom(implementationType))
            {
                var serviceDescriptor = new ServiceDescriptor(implementationType, implementationType, serviceLifetime);
                services.Add(serviceDescriptor);
            }
        }
    }

    /// <summary>
    /// Gets statistics about registered services for debugging/logging.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Assemblies that were scanned.</param>
    /// <returns>Registration statistics.</returns>
    public static ServiceRegistrationStats GetRegistrationStats(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        var stats = new ServiceRegistrationStats();

        foreach (var assembly in assemblies)
        {
            stats.TransientCount += GetServiceCount<ITransientService>(assembly);
            stats.ScopedCount += GetServiceCount<IScopedService>(assembly);
            stats.SingletonCount += GetServiceCount<ISingletonService>(assembly);
            stats.ScannedAssemblies.Add(assembly.GetName().Name ?? "Unknown");
        }

        return stats;
    }

    private static int GetServiceCount<TLifetimeMarker>(Assembly assembly) where TLifetimeMarker : class
    {
        return assembly.GetExportedTypes()
            .Count(type => type.IsClass && !type.IsAbstract &&
                          type.GetInterfaces().Any(i => typeof(TLifetimeMarker).IsAssignableFrom(i)));
    }
}

/// <summary>
/// Statistics about service registration.
/// </summary>
public class ServiceRegistrationStats
{
    /// <summary>
    /// Number of transient services registered.
    /// </summary>
    public int TransientCount { get; set; }

    /// <summary>
    /// Number of scoped services registered.
    /// </summary>
    public int ScopedCount { get; set; }

    /// <summary>
    /// Number of singleton services registered.
    /// </summary>
    public int SingletonCount { get; set; }

    /// <summary>
    /// Names of assemblies that were scanned.
    /// </summary>
    public List<string> ScannedAssemblies { get; set; } = [];

    /// <summary>
    /// Total number of services registered.
    /// </summary>
    public int TotalCount => TransientCount + ScopedCount + SingletonCount;

    /// <summary>
    /// Returns a formatted string with registration statistics.
    /// </summary>
    public override string ToString()
    {
        return $"Registered {TotalCount} services: {TransientCount} Transient, {ScopedCount} Scoped, {SingletonCount} Singleton from assemblies: {string.Join(", ", ScannedAssemblies)}";
    }
}
