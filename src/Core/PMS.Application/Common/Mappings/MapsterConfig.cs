using System.Reflection;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;

namespace PMS.Application.Common.Mappings;

/// <summary>
/// Mapster configuration for object mapping.
/// </summary>
public static class MapsterConfig
{
    /// <summary>
    /// Registers Mapster configuration and services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection RegisterMapsterConfiguration(this IServiceCollection services)
    {
        // Get the global TypeAdapterConfig
        var config = TypeAdapterConfig.GlobalSettings;

        // Configure default settings
        config.Default
            .PreserveReference(true)
            .ShallowCopyForSameType(true);

        // Apply convention-based mappings from IMapFrom<T> and IMapTo<T>
        ApplyMappingsFromAssembly(config, Assembly.GetExecutingAssembly());

        // Scan for IRegister configurations
        config.Scan(Assembly.GetExecutingAssembly());

        // Add Mapster to DI
        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();

        return services;
    }

    /// <summary>
    /// Registers Mapster configuration from specified assemblies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Assemblies to scan for mappings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection RegisterMapsterConfiguration(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        var config = TypeAdapterConfig.GlobalSettings;

        config.Default
            .PreserveReference(true)
            .ShallowCopyForSameType(true);

        // Always include the executing assembly
        ApplyMappingsFromAssembly(config, Assembly.GetExecutingAssembly());
        config.Scan(Assembly.GetExecutingAssembly());

        // Scan additional assemblies
        foreach (var assembly in assemblies)
        {
            ApplyMappingsFromAssembly(config, assembly);
            config.Scan(assembly);
        }

        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();

        return services;
    }

    private static void ApplyMappingsFromAssembly(TypeAdapterConfig config, Assembly assembly)
    {
        var types = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .ToList();

        foreach (var type in types)
        {
            // Find all IMapFrom<T> interfaces
            var mapFromInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMapFrom<>))
                .ToList();

            foreach (var mapFromInterface in mapFromInterfaces)
            {
                var sourceType = mapFromInterface.GetGenericArguments()[0];

                // Create mapping configuration from source to current type
                config.ForType(sourceType, type);

                // Check if type has ConfigureMapping method with custom implementation
                var configureMappingMethod = type.GetMethod("ConfigureMapping",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);

                if (configureMappingMethod != null && configureMappingMethod.DeclaringType == type)
                {
                    // Create instance and call ConfigureMapping
                    var instance = Activator.CreateInstance(type);
                    configureMappingMethod.Invoke(instance, null);
                }
            }

        }
    }

    /// <summary>
    /// Adds custom mappings from IRegister implementations.
    /// </summary>
    /// <param name="config">The TypeAdapterConfig.</param>
    public static void AddCustomMappings(this TypeAdapterConfig config)
    {
        // Apply all IRegister configurations
        var registers = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(x => typeof(IRegister).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
            .Select(Activator.CreateInstance)
            .Cast<IRegister>();

        foreach (var register in registers)
        {
            register.Register(config);
        }
    }
}
