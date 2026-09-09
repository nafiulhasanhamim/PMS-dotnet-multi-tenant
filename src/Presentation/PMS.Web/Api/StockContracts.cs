namespace PMS.Web.Api;

// ═══════════════════════════════════════════════════════════════════════════════════════════
// Module 3 wire contracts.
//
// Enum values must match the server's NUMERIC values — the API serialises enums as integers,
// the convention Module 1 established and every client here relies on.
// ═══════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Which of a product's selling units a quantity or price is expressed in.
///
/// <para>A product always has Base. Mid and Large exist only if that product defines them,
/// which is why every unit dropdown in this module is built from the selected product rather
/// than from this enum.</para>
/// </summary>
public enum UnitLevel
{
    Base = 0,
    Mid = 1,
    Large = 2,
}

/// <summary>Why a batch quantity changed. A sale is deliberately not one of these.</summary>
public enum AdjustmentType
{
    Add = 0,
    Remove = 1,
    Correction = 2,
}

/// <summary>How a product stands against its reorder level. Computed server-side.</summary>
public enum StockStatus
{
    Ok = 0,
    Low = 1,
    OutOfStock = 2,
}

/// <summary>Where a batch stands against its expiry date. Computed server-side.</summary>
public enum ExpiryState
{
    /// <summary>No expiry date — diapers, syringes, dressings.</summary>
    NotApplicable = 0,
    Ok = 1,
    ExpiringSoon = 2,
    Expired = 3,
}

public enum StockStatusFilter
{
    All = 0,
    InStock = 1,
    OutOfStock = 2,
    LowStock = 3,
}

public enum ExpiryStatusFilter
{
    All = 0,
    ExpiringSoon = 1,
    Expired = 2,
}

public enum StockProductTypeFilter
{
    All = 0,
    Medicine = 1,
    Other = 2,
}

/// <param name="TotalQuantityInBaseUnits">
/// Sellable stock. Expired stock is reported in <paramref name="ExpiredQuantityInBaseUnits"/>
/// instead of being added in — see the server DTO for why that is the only truthful split.
/// </param>
public sealed record StockListItem(
    Guid ProductId,
    string BrandName,
    string? GenericName,
    ProductType ProductType,
    bool IsAntibiotic,
    string BaseUnitName,
    int TotalQuantityInBaseUnits,
    string FormattedQuantity,
    int BatchCount,
    DateOnly? NearestExpiryDate,
    int? DaysUntilNearestExpiry,
    int ReorderLevel,
    StockStatus Status,
    int ExpiredQuantityInBaseUnits,
    string? FormattedExpiredQuantity,
    bool HasExpiredStock,
    ExpiryState NearestExpiryState);

/// <param name="PurchasePricePerBaseUnit">
/// Null for an Employee — the API projection never reads it for that role, so the cost does
/// not cross the wire. The absent value is the control; the missing column is a consequence.
/// </param>
public sealed record BatchModel(
    Guid Id,
    Guid ProductId,
    string ProductBrandName,
    string BatchNumber,
    DateOnly? ExpiryDate,
    DateOnly? ManufactureDate,
    int? DaysUntilExpiry,
    ExpiryState ExpiryState,
    decimal? PurchasePricePerBaseUnit,
    int QuantityInBaseUnits,
    string FormattedQuantity,
    int InitialQuantityInBaseUnits,
    string FormattedInitialQuantity,
    Guid? SupplierId,
    string? SupplierNameText,
    string? Notes,
    bool IsActive,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc,
    bool IsDepleted);

/// <param name="ActiveBatches">
/// Batches holding stock, in FEFO order — the batch to sell next is the first row, and an
/// expired one that still holds stock sits at the top in red because it is the most urgent
/// thing on the page.
/// </param>
public sealed record ProductStockModel(
    Guid ProductId,
    string BrandName,
    string? GenericName,
    string? Strength,
    ProductType ProductType,
    bool IsAntibiotic,
    string BaseUnitName,
    string PackingSummary,
    decimal PricePerBase,
    int ReorderLevel,
    int TotalQuantityInBaseUnits,
    string FormattedQuantity,
    int BatchCount,
    DateOnly? NearestExpiryDate,
    int? DaysUntilNearestExpiry,
    StockStatus Status,
    IReadOnlyList<BatchModel> ActiveBatches,
    int DepletedBatchCount,
    IReadOnlyList<BatchModel> DepletedBatches,
    int ExpiredQuantityInBaseUnits,
    string? FormattedExpiredQuantity,
    bool IsBelowReorderLevel,
    bool HasExpiredStock,
    ExpiryState NearestExpiryState);

public sealed record StockAdjustmentModel(
    Guid Id,
    Guid BatchId,
    string BatchNumber,
    AdjustmentType AdjustmentType,
    int QuantityChangeInBaseUnits,
    string FormattedQuantityChange,
    int QuantityBeforeInBaseUnits,
    int QuantityAfterInBaseUnits,
    string Reason,
    Guid AdjustedByUserId,
    string? AdjustedByName,
    DateTime CreatedOnUtc);

/// <param name="SellsAtALoss">
/// The server-side answer to whether this batch costs more than the product sells for. The
/// form asks for confirmation before posting, from the price it already has on screen; this
/// also catches the case where the product was repriced in between.
/// </param>
public sealed record BatchCreated(
    BatchModel Batch,
    bool SellsAtALoss,
    decimal ProductPricePerBaseUnit,
    string? Warning);

public sealed record BatchAdjusted(
    BatchModel Batch,
    StockAdjustmentModel Adjustment);

// --- requests ---

public sealed record CreateBatchRequest(
    Guid ProductId,
    string BatchNumber,
    DateOnly? ExpiryDate,
    DateOnly? ManufactureDate,
    decimal Quantity,
    UnitLevel QuantityUnit,
    decimal PurchasePrice,
    UnitLevel PurchasePriceUnit,
    Guid? SupplierId,
    string? SupplierNameText,
    string? Notes);

/// <param name="QuantityInBaseUnits">
/// Sent as the batch's current value so the server accepts it as a no-op. Sending a different
/// one is refused with a message pointing at the adjustment screen — deliberately, because a
/// silently ignored field would let a client believe a quantity change had worked.
/// </param>
public sealed record UpdateBatchRequest(
    string BatchNumber,
    DateOnly? ExpiryDate,
    DateOnly? ManufactureDate,
    decimal PurchasePrice,
    UnitLevel PurchasePriceUnit,
    Guid? SupplierId,
    string? SupplierNameText,
    string? Notes,
    int? QuantityInBaseUnits);

/// <param name="Quantity">
/// For Add and Remove, how much to move. For Correction, the true total — the server computes
/// the difference.
/// </param>
public sealed record AdjustBatchRequest(
    AdjustmentType AdjustmentType,
    decimal Quantity,
    UnitLevel QuantityUnit,
    string Reason,
    bool AcknowledgeExpiringBatch);
