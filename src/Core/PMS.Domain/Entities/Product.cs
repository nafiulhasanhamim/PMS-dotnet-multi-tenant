using Ardalis.GuardClauses;
using PMS.Domain.Exceptions;
using PMS.Domain.ValueObjects;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;

namespace PMS.Domain.Entities;

/// <summary>
/// Represents a product in the catalog.
/// This is an aggregate root.
/// </summary>
public sealed class Product : BaseAuditableAggregateRoot<Guid>, ISoftDelete
{
    /// <summary>
    /// Gets the product name.
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// Gets the product description.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Gets the product SKU (Stock Keeping Unit).
    /// </summary>
    public string Sku { get; private set; } = null!;

    /// <summary>
    /// Gets the product price.
    /// </summary>
    public Money Price { get; private set; } = null!;

    /// <summary>
    /// Gets the available stock quantity.
    /// </summary>
    public int StockQuantity { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the product is active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTime? DeletedOnUtc { get; set; }

    /// <inheritdoc />
    public string? DeletedBy { get; set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private Product() { }

    /// <summary>
    /// Creates a new product.
    /// </summary>
    /// <param name="name">The product name.</param>
    /// <param name="sku">The product SKU.</param>
    /// <param name="price">The product price.</param>
    /// <param name="description">Optional product description.</param>
    public Product(string name, string sku, Money price, string? description = null)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.NullOrWhiteSpace(sku, nameof(sku));
        Guard.Against.Null(price, nameof(price));

        Id = Guid.NewGuid();
        Name = name.Trim();
        Sku = sku.Trim().ToUpperInvariant();
        Price = price;
        Description = description?.Trim();
        StockQuantity = 0;
        IsActive = true;
    }

    /// <summary>
    /// Updates the product details.
    /// </summary>
    public void UpdateDetails(string name, string sku, Money price, string? description)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.NullOrWhiteSpace(sku, nameof(sku));
        Guard.Against.Null(price, nameof(price));

        Name = name.Trim();
        Sku = sku.Trim().ToUpperInvariant();
        Price = price;
        Description = description?.Trim();
    }

    /// <summary>
    /// Updates the product price.
    /// </summary>
    public void UpdatePrice(Money newPrice)
    {
        Price = newPrice;
    }

    /// <summary>
    /// Adds stock to the product.
    /// </summary>
    /// <param name="quantity">The quantity to add.</param>
    public void AddStock(int quantity)
    {
        Guard.Against.NegativeOrZero(quantity, nameof(quantity));

        StockQuantity += quantity;
    }

    /// <summary>
    /// Removes stock from the product.
    /// </summary>
    /// <param name="quantity">The quantity to remove.</param>
    public void RemoveStock(int quantity)
    {
        Guard.Against.NegativeOrZero(quantity, nameof(quantity));

        if (quantity > StockQuantity)
            throw new InsufficientStockException(Id, quantity, StockQuantity);

        StockQuantity -= quantity;
    }

    /// <summary>
    /// Checks if the product has sufficient stock.
    /// </summary>
    public bool HasSufficientStock(int quantity) => StockQuantity >= quantity;

    /// <summary>
    /// Activates the product.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Deactivates the product.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }
}
