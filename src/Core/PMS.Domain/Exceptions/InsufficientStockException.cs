namespace PMS.Domain.Exceptions;

/// <summary>
/// Exception thrown when there is insufficient stock for an operation.
/// </summary>
public class InsufficientStockException : DomainException
{
    /// <summary>
    /// Gets the product ID.
    /// </summary>
    public Guid ProductId { get; }

    /// <summary>
    /// Gets the requested quantity.
    /// </summary>
    public int RequestedQuantity { get; }

    /// <summary>
    /// Gets the available quantity.
    /// </summary>
    public int AvailableQuantity { get; }

    /// <summary>
    /// Creates a new insufficient stock exception.
    /// </summary>
    /// <param name="productId">The product ID.</param>
    /// <param name="requestedQuantity">The requested quantity.</param>
    /// <param name="availableQuantity">The available quantity.</param>
    public InsufficientStockException(Guid productId, int requestedQuantity, int availableQuantity)
        : base($"Insufficient stock for product '{productId}'. Requested: {requestedQuantity}, Available: {availableQuantity}.")
    {
        ProductId = productId;
        RequestedQuantity = requestedQuantity;
        AvailableQuantity = availableQuantity;
    }
}
