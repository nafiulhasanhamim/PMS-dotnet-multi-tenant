using PMS.SharedKernel.Common;

namespace PMS.SharedKernel.Interfaces;

/// <summary>
/// Dispatches domain events to their respective handlers.
/// Typically implemented using MediatR to publish notifications.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Dispatches a collection of domain events.
    /// </summary>
    /// <param name="domainEvents">The events to dispatch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DispatchEventsAsync(IEnumerable<DomainEvent> domainEvents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches domain events from all entities tracked by the context.
    /// Call this before or after SaveChanges depending on your strategy.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DispatchAndClearEventsAsync(CancellationToken cancellationToken = default);
}
