namespace PMS.Web.Api;

// ═══════════════════════════════════════════════════════════════════════════════════════════
// Module 6 wire contracts.
//
// Enum values must match the server's NUMERIC values — the API serialises enums as integers,
// the convention Module 1 established and every client here relies on.
// ═══════════════════════════════════════════════════════════════════════════════════════════

/// <summary>How urgent a row is. Decided server-side so the pages cannot disagree.</summary>
public enum AlertSeverity
{
    Normal = 0,
    Warning = 1,
    Critical = 2,
}

/// <summary>Whether a product is merely low or actually out.</summary>
public enum LowStockStatus
{
    Low = 0,
    OutOfStock = 1,
}

public enum LowStockStatusFilter
{
    All = 0,
    Low = 1,
    OutOfStock = 2,
}

/// <summary>
/// One batch on the expiring or the expired list.
/// </summary>
/// <param name="DaysUntilExpiry">
/// Negative once the date has passed. One shape serves both lists: the expiring page reads it as
/// days remaining, the expired page as days overdue.
/// </param>
public sealed record ExpiringBatch(
    Guid BatchId,
    Guid ProductId,
    string BrandName,
    string? GenericName,
    ProductType ProductType,
    string BatchNumber,
    DateOnly ExpiryDate,
    int DaysUntilExpiry,
    int QuantityInBaseUnits,
    string FormattedQuantity,
    string BaseUnitName,
    AlertSeverity Severity,
    Guid? SupplierId,
    string? SupplierNameText)
{
    public bool IsExpired => DaysUntilExpiry < 0;

    public int DaysOverdue => DaysUntilExpiry < 0 ? -DaysUntilExpiry : 0;

    /// <summary>
    /// Whether this batch can be handed back to whoever supplied it rather than written off.
    /// Decides which action the expired page offers — see the frontend doc.
    /// </summary>
    public bool CameFromARecordedPurchase => SupplierId is not null;
}

public sealed record LowStockProduct(
    Guid ProductId,
    string BrandName,
    string? GenericName,
    ProductType ProductType,
    int TotalQuantityInBaseUnits,
    string FormattedQuantity,
    int ReorderLevel,
    string BaseUnitName,
    LowStockStatus Status,
    int ExpiredQuantityInBaseUnits,
    string? FormattedExpiredQuantity)
{
    public bool IsOutOfStock => Status == LowStockStatus.OutOfStock;

    /// <summary>
    /// True when the product is out of sellable stock but the shelf is not empty — the case the
    /// list has to explain, or somebody looks at a full shelf and assumes the alert is wrong.
    /// </summary>
    public bool IsOutButHoldingExpiredStock =>
        IsOutOfStock && ExpiredQuantityInBaseUnits > 0;
}

public sealed record NearestExpiry(
    Guid ProductId,
    string BrandName,
    string BatchNumber,
    DateOnly ExpiryDate,
    int DaysUntilExpiry);

public sealed record AlertSummary(
    int ExpiringSoon,
    int Expired,
    int LowStock,
    int OutOfStock,
    int ExpiryWindowDays,
    NearestExpiry? NearestExpiry)
{
    /// <summary>
    /// An empty summary, used when the call fails. A dashboard that renders four zeroes and a
    /// banner is better than one that renders nothing: the rest of the page still works, and the
    /// alternative is a failed alert lookup taking down the screen everything else lives on.
    /// </summary>
    public static AlertSummary Empty { get; } = new(0, 0, 0, 0, 90, null);

    /// <summary>What the sidebar badge counts: the two categories that need action today.</summary>
    public int UrgentCount => Expired + OutOfStock;

    public bool HasAnything => ExpiringSoon + Expired + LowStock + OutOfStock > 0;
}
