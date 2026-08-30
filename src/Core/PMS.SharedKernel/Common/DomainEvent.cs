using MediatR;

namespace PMS.SharedKernel.Common;

/// <summary>
/// Base class for all domain events.
/// Domain events represent something that happened in the domain
/// and are used to communicate changes across aggregates.
/// </summary>
public abstract class DomainEvent : INotification
{
    /// <summary>
    /// Gets the unique identifier for this event.
    /// </summary>
    public Guid EventId { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets the UTC timestamp when this event occurred.
    /// </summary>
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the type name of this event.
    /// </summary>
    public string EventType => GetType().Name;
}
