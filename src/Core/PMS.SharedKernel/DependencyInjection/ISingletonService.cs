namespace PMS.SharedKernel.DependencyInjection;

/// <summary>
/// Marker interface for services that should be registered with Singleton lifetime.
/// Singleton services are created once and shared across all requests.
/// Use for services that are expensive to create or maintain application-wide state.
/// </summary>
/// <remarks>
/// <para>
/// <b>Usage Pattern:</b> Service interfaces should inherit from this marker interface,
/// not the implementation class. This ensures the lifetime is defined at the contract level.
/// </para>
/// <para>
/// <b>Warning:</b> Singleton services must be thread-safe. They cannot depend on Scoped services.
/// </para>
/// <para>
/// <b>Example:</b>
/// <code>
/// // Define service interface that inherits from marker
/// public interface ICacheService : ISingletonService
/// {
///     Task&lt;T?&gt; GetAsync&lt;T&gt;(string key);
///     Task SetAsync&lt;T&gt;(string key, T value, TimeSpan? expiry = null);
/// }
///
/// // Implementation only implements the service interface
/// public class CacheService : ICacheService
/// {
///     public Task&lt;T?&gt; GetAsync&lt;T&gt;(string key) { ... }
///     public Task SetAsync&lt;T&gt;(string key, T value, TimeSpan? expiry = null) { ... }
/// }
/// </code>
/// </para>
/// </remarks>
public interface ISingletonService
{
}
