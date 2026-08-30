using PMS.SharedKernel.Common;

namespace PMS.Domain.Events;

/// <summary>
/// Domain event raised when an item is added to an order.
/// </summary>
public sealed class OrderItemAddedEvent : DomainEvent
{
    /// <summary>
    /// Gets the ID of the order.
    /// </summary>
    public Guid OrderId { get; }

    /// <summary>
    /// Gets the ID of the product added.
    /// </summary>
    public Guid ProductId { get; }

    /// <summary>
    /// Gets the quantity added.
    /// </summary>
    public int Quantity { get; }

    /// <summary>
    /// Gets the unit price at the time of adding.
    /// </summary>
    public decimal UnitPrice { get; }

    public OrderItemAddedEvent(Guid orderId, Guid productId, int quantity, decimal unitPrice)
    {
        OrderId = orderId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
