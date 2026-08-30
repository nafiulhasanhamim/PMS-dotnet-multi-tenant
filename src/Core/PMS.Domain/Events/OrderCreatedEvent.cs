using PMS.SharedKernel.Common;

namespace PMS.Domain.Events;

/// <summary>
/// Domain event raised when a new order is created.
/// </summary>
public sealed class OrderCreatedEvent : DomainEvent
{
    /// <summary>
    /// Gets the ID of the created order.
    /// </summary>
    public Guid OrderId { get; }

    /// <summary>
    /// Gets the ID of the customer who placed the order.
    /// </summary>
    public Guid CustomerId { get; }

    /// <summary>
    /// Gets the total amount of the order.
    /// </summary>
    public decimal TotalAmount { get; }

    public OrderCreatedEvent(Guid orderId, Guid customerId, decimal totalAmount)
    {
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
    }
}
