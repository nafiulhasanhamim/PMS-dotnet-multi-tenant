using Ardalis.GuardClauses;
using PMS.Domain.Enums;
using PMS.Domain.Events;
using PMS.Domain.Exceptions;
using PMS.Domain.ValueObjects;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;

namespace PMS.Domain.Entities;

/// <summary>
/// Represents a customer order.
/// This is an aggregate root.
/// </summary>
public sealed class Order : BaseAuditableAggregateRoot<Guid>, ISoftDelete
{
    private readonly List<OrderItem> _items = [];

    /// <summary>
    /// Gets the customer ID who placed the order.
    /// </summary>
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// Gets the customer who placed the order.
    /// </summary>
    public Customer? Customer { get; private set; }

    /// <summary>
    /// Gets the order number.
    /// </summary>
    public string OrderNumber { get; private set; } = null!;

    /// <summary>
    /// Gets the order date.
    /// </summary>
    public DateTime OrderDateUtc { get; private set; }

    /// <summary>
    /// Gets the order status.
    /// </summary>
    public OrderStatus Status { get; private set; }

    /// <summary>
    /// Gets the shipping address for the order.
    /// </summary>
    public Address ShippingAddress { get; private set; } = null!;

    /// <summary>
    /// Gets the order notes.
    /// </summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// Gets the order items.
    /// </summary>
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Gets the subtotal (sum of all item totals).
    /// </summary>
    public Money Subtotal => CalculateSubtotal();

    /// <summary>
    /// Gets the total amount of the order.
    /// </summary>
    public Money Total => Subtotal;

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTime? DeletedOnUtc { get; set; }

    /// <inheritdoc />
    public string? DeletedBy { get; set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private Order() { }

    /// <summary>
    /// Creates a new order.
    /// </summary>
    /// <param name="customerId">The customer ID.</param>
    /// <param name="shippingAddress">The shipping address.</param>
    /// <param name="notes">Optional order notes.</param>
    public Order(Guid customerId, Address shippingAddress, string? notes = null)
    {
        Guard.Against.Default(customerId, nameof(customerId));
        Guard.Against.Null(shippingAddress, nameof(shippingAddress));

        Id = Guid.NewGuid();
        CustomerId = customerId;
        OrderNumber = GenerateOrderNumber();
        OrderDateUtc = DateTime.UtcNow;
        Status = OrderStatus.Pending;
        ShippingAddress = shippingAddress;
        Notes = notes?.Trim();
    }

    /// <summary>
    /// Adds an item to the order.
    /// </summary>
    /// <param name="productId">The product ID.</param>
    /// <param name="productName">The product name (for denormalization).</param>
    /// <param name="unitPrice">The unit price at order time.</param>
    /// <param name="quantity">The quantity.</param>
    public void AddItem(Guid productId, string productName, Money unitPrice, int quantity)
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException("Cannot modify a confirmed order.");

        Guard.Against.Default(productId, nameof(productId));
        Guard.Against.NullOrWhiteSpace(productName, nameof(productName));
        Guard.Against.Null(unitPrice, nameof(unitPrice));
        Guard.Against.NegativeOrZero(quantity, nameof(quantity));

        var existingItem = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existingItem is not null)
        {
            existingItem.IncreaseQuantity(quantity);
        }
        else
        {
            var item = new OrderItem(Id, productId, productName, unitPrice, quantity);
            _items.Add(item);
        }

        AddDomainEvent(new OrderItemAddedEvent(Id, productId, quantity, unitPrice.Amount));
    }

    /// <summary>
    /// Removes an item from the order.
    /// </summary>
    /// <param name="productId">The product ID to remove.</param>
    public void RemoveItem(Guid productId)
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException("Cannot modify a confirmed order.");

        var item = _items.FirstOrDefault(i => i.ProductId == productId);
        if (item is null)
            throw new DomainException("Item not found in order.");

        _items.Remove(item);
    }

    /// <summary>
    /// Confirms the order.
    /// </summary>
    public void Confirm()
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException("Only pending orders can be confirmed.");

        if (_items.Count == 0)
            throw new DomainException("Cannot confirm an empty order.");

        var oldStatus = Status;
        Status = OrderStatus.Confirmed;

        AddDomainEvent(new OrderStatusChangedEvent(Id, oldStatus, Status));
        AddDomainEvent(new OrderCreatedEvent(Id, CustomerId, Total.Amount));
    }

    /// <summary>
    /// Starts processing the order.
    /// </summary>
    public void StartProcessing()
    {
        if (Status != OrderStatus.Confirmed)
            throw new DomainException("Only confirmed orders can be processed.");

        var oldStatus = Status;
        Status = OrderStatus.Processing;

        AddDomainEvent(new OrderStatusChangedEvent(Id, oldStatus, Status));
    }

    /// <summary>
    /// Ships the order.
    /// </summary>
    public void Ship()
    {
        if (Status != OrderStatus.Processing)
            throw new DomainException("Only processing orders can be shipped.");

        var oldStatus = Status;
        Status = OrderStatus.Shipped;

        AddDomainEvent(new OrderStatusChangedEvent(Id, oldStatus, Status));
    }

    /// <summary>
    /// Marks the order as delivered.
    /// </summary>
    public void MarkAsDelivered()
    {
        if (Status != OrderStatus.Shipped)
            throw new DomainException("Only shipped orders can be marked as delivered.");

        var oldStatus = Status;
        Status = OrderStatus.Delivered;

        AddDomainEvent(new OrderStatusChangedEvent(Id, oldStatus, Status));
    }

    /// <summary>
    /// Cancels the order.
    /// </summary>
    public void Cancel()
    {
        if (Status == OrderStatus.Delivered)
            throw new DomainException("Delivered orders cannot be cancelled.");

        if (Status == OrderStatus.Cancelled)
            throw new DomainException("Order is already cancelled.");

        var oldStatus = Status;
        Status = OrderStatus.Cancelled;

        AddDomainEvent(new OrderStatusChangedEvent(Id, oldStatus, Status));
    }

    private Money CalculateSubtotal()
    {
        if (_items.Count == 0)
            return Money.Zero("USD");

        return _items.Aggregate(
            Money.Zero(_items[0].UnitPrice.Currency),
            (sum, item) => sum + item.Total);
    }

    private static string GenerateOrderNumber()
    {
        return $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }
}
