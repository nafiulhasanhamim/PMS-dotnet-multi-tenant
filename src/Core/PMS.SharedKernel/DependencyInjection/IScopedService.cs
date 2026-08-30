namespace PMS.SharedKernel.DependencyInjection;

/// <summary>
/// Marker interface for services that should be registered with Scoped lifetime.
/// Scoped services are created once per request/scope.
/// Use for services that maintain state within a single request (e.g., DbContext, Unit of Work).
/// </summary>
/// <remarks>
/// <para>
/// <b>Usage Pattern:</b> Service interfaces should inherit from this marker interface,
/// not the implementation class. This ensures the lifetime is defined at the contract level.
/// </para>
/// <para>
/// <b>Example:</b>
/// <code>
/// // Define service interface that inherits from marker
/// public interface IOrderService : IScopedService
/// {
///     Task&lt;Order&gt; CreateOrderAsync(CreateOrderDto dto);
///     Task&lt;Order?&gt; GetOrderAsync(Guid id);
/// }
///
/// // Implementation only implements the service interface
/// public class OrderService : IOrderService
/// {
///     public Task&lt;Order&gt; CreateOrderAsync(CreateOrderDto dto) { ... }
///     public Task&lt;Order?&gt; GetOrderAsync(Guid id) { ... }
/// }
/// </code>
/// </para>
/// </remarks>
public interface IScopedService
{
}
