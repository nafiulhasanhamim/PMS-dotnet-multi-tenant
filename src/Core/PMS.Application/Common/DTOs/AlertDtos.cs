using PMS.Domain.Enums;

namespace PMS.Application.Common.DTOs;

/// <summary>
/// How urgent a row is, decided by how many days are left.
///
/// <para>Computed once, server-side, so the list page and the dashboard cannot disagree about
/// what counts as urgent — and so the thresholds live with the rest of the stock policy rather
/// than as numbers in a Razor file.</para>
/// </summary>
public enum AlertSeverity
{
    /// <summary>Inside the window but not pressing.</summary>
    Normal = 0,

    /// <summary>Within the warning threshold. Amber.</summary>
    Warning = 1,

    /// <summary>Within the critical threshold, or already past. Red.</summary>
    Critical = 2,
}

/// <summary>
/// One batch on the expiring or expired list.
/// </summary>
/// <param name="DaysUntilExpiry">
/// Negative once the date has passed, which is what makes one shape serve both lists: the
/// expiring page reads it as days remaining, the expired page negates it and calls it days
/// overdue. Never null — a batch with no expiry date appears on neither list, so it never
/// reaches this record.
/// </param>
public sealed record ExpiringBatchDto(
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

    /// <summary>
    /// Set when the delivery was recorded against a supplier, which decides what the expired
    /// page offers as the next action — see the frontend doc.
    /// </summary>
    Guid? SupplierId,
    string? SupplierNameText)
{
    public bool IsExpired => DaysUntilExpiry < 0;

    /// <summary>Positive days past the date. Only meaningful when <see cref="IsExpired"/>.</summary>
    public int DaysOverdue => DaysUntilExpiry < 0 ? -DaysUntilExpiry : 0;

    /// <summary>
    /// Whether this batch can be handed back to whoever supplied it, rather than written off.
    ///
    /// <para>Decided in one place because two screens ask it. Note it is about a recorded
    /// purchase, not about the presence of a name typed into a free-text field: a batch entered
    /// by hand with "local supplier" scribbled on it has nothing to return against.</para>
    /// </summary>
    public bool CameFromARecordedPurchase => SupplierId is not null;
}

/// <summary>Whether a product is merely low or actually out.</summary>
public enum LowStockStatus
{
    /// <summary>At or below the reorder level, with some stock left.</summary>
    Low = 0,

    /// <summary>Nothing sellable at all.</summary>
    OutOfStock = 1,
}

/// <summary>Filter for the low-stock list.</summary>
public enum LowStockStatusFilter
{
    All = 0,
    Low = 1,
    OutOfStock = 2,
}

/// <summary>
/// One product on the low-stock list.
/// </summary>
/// <param name="TotalQuantityInBaseUnits">
/// Sellable stock only. Expired batches are excluded, which is the whole point — see the
/// module doc.
/// </param>
public sealed record LowStockProductDto(
    Guid ProductId,
    string BrandName,
    string? GenericName,
    ProductType ProductType,
    int TotalQuantityInBaseUnits,
    string FormattedQuantity,
    int ReorderLevel,
    string BaseUnitName,
    LowStockStatus Status,

    /// <summary>
    /// Expired stock this product still holds, if any. Not counted in the total above, and
    /// reported so the list can say why a product with a full shelf is out of stock.
    /// </summary>
    int ExpiredQuantityInBaseUnits,
    string? FormattedExpiredQuantity);

/// <summary>The nearest thing to expiry, for the dashboard preview line.</summary>
public sealed record NearestExpiryDto(
    Guid ProductId,
    string BrandName,
    string BatchNumber,
    DateOnly ExpiryDate,
    int DaysUntilExpiry);

/// <summary>
/// The four counts behind the dashboard cards, plus the nearest-expiry preview.
/// </summary>
/// <param name="ExpiryWindowDays">
/// The window the expiring count was taken over, echoed back so the card can say "in the next
/// 90 days" without the client holding its own copy of the number.
/// </param>
public sealed record AlertSummaryDto(
    int ExpiringSoon,
    int Expired,
    int LowStock,
    int OutOfStock,
    int ExpiryWindowDays,
    NearestExpiryDto? NearestExpiry)
{
    /// <summary>
    /// What the sidebar badge shows: the two categories that mean somebody has to act today.
    ///
    /// <para>Expiring stock and merely-low stock are things to plan around. Expired stock is
    /// money already lost sitting on a shelf, and an out-of-stock product is a sale being turned
    /// away right now — so those are the two that earn a number next to the nav item.</para>
    /// </summary>
    public int UrgentCount => Expired + OutOfStock;

    public bool HasAnything => ExpiringSoon + Expired + LowStock + OutOfStock > 0;
}
