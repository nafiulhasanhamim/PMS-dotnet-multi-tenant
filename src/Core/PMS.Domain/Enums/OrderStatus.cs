namespace PMS.Domain.Enums;

/// <summary>
/// Represents the status of an order in its lifecycle.
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// Order has been created but not yet confirmed.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Order has been confirmed and is being processed.
    /// </summary>
    Confirmed = 1,

    /// <summary>
    /// Order is being prepared for shipment.
    /// </summary>
    Processing = 2,

    /// <summary>
    /// Order has been shipped.
    /// </summary>
    Shipped = 3,

    /// <summary>
    /// Order has been delivered to the customer.
    /// </summary>
    Delivered = 4,

    /// <summary>
    /// Order has been cancelled.
    /// </summary>
    Cancelled = 5,

    /// <summary>
    /// Order has been refunded.
    /// </summary>
    Refunded = 6
}
