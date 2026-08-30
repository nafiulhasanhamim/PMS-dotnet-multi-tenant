using PMS.Domain.Enums;
using PMS.SharedKernel.Common;

namespace PMS.Domain.Events;

/// <summary>
/// Domain event raised when an order's status changes.
/// </summary>
public sealed class OrderStatusChangedEvent : DomainEvent
{
    /// <summary>
    /// Gets the ID of the order.
    /// </summary>
    public Guid OrderId { get; }

    /// <summary>
    /// Gets the previous status.
    /// </summary>
    public OrderStatus OldStatus { get; }

    /// <summary>
    /// Gets the new status.
    /// </summary>
    public OrderStatus NewStatus { get; }

    public OrderStatusChangedEvent(Guid orderId, OrderStatus oldStatus, OrderStatus newStatus)
    {
        OrderId = orderId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }
}
