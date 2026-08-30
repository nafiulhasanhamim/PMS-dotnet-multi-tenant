using Ardalis.GuardClauses;
using PMS.Domain.ValueObjects;
using PMS.SharedKernel.Common;

namespace PMS.Domain.Entities;

/// <summary>
/// Represents an item in an order.
/// This is a child entity of Order aggregate.
/// </summary>
public sealed class OrderItem : BaseEntity<Guid>
{
    /// <summary>
    /// Gets the order ID this item belongs to.
    /// </summary>
    public Guid OrderId { get; private set; }

    /// <summary>
    /// Gets the product ID.
    /// </summary>
    public Guid ProductId { get; private set; }

    /// <summary>
    /// Gets the product name (denormalized for historical accuracy).
    /// </summary>
    public string ProductName { get; private set; } = null!;

    /// <summary>
    /// Gets the unit price at the time of order.
    /// </summary>
    public Money UnitPrice { get; private set; } = null!;

    /// <summary>
    /// Gets the quantity ordered.
    /// </summary>
    public int Quantity { get; private set; }

    /// <summary>
    /// Gets the total price for this line item.
    /// </summary>
    public Money Total => UnitPrice * Quantity;

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private OrderItem() { }

    /// <summary>
    /// Creates a new order item.
    /// </summary>
    internal OrderItem(Guid orderId, Guid productId, string productName, Money unitPrice, int quantity)
    {
        Guard.Against.Default(orderId, nameof(orderId));
        Guard.Against.Default(productId, nameof(productId));
        Guard.Against.NullOrWhiteSpace(productName, nameof(productName));
        Guard.Against.Null(unitPrice, nameof(unitPrice));
        Guard.Against.NegativeOrZero(quantity, nameof(quantity));

        Id = Guid.NewGuid();
        OrderId = orderId;
        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    /// <summary>
    /// Increases the quantity of this item.
    /// </summary>
    internal void IncreaseQuantity(int additionalQuantity)
    {
        Guard.Against.NegativeOrZero(additionalQuantity, nameof(additionalQuantity));

        Quantity += additionalQuantity;
    }

    /// <summary>
    /// Updates the quantity of this item.
    /// </summary>
    internal void UpdateQuantity(int newQuantity)
    {
        Guard.Against.NegativeOrZero(newQuantity, nameof(newQuantity));

        Quantity = newQuantity;
    }
}
