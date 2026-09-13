using PMS.Domain.Enums;

namespace PMS.Application.Common.DTOs;

/// <summary>
/// One row of the stock list — <b>one product, not one batch</b>.
///
/// <para>A pharmacist scanning the shelf asks "how much Napa do I have", not "how much of
/// batch B-2451". The aggregates here are across that product's batches, and the batch-level
/// view is one click away on the detail page.</para>
/// </summary>
/// <param name="NearestExpiryDate">
/// The soonest expiry among batches that have one. Batches with no expiry are excluded from
/// the calculation rather than treated as far-future, and a product whose stock all has no
/// expiry reports null — rendered as an em dash, not as "safe for ever".
/// </param>
/// <param name="FormattedQuantity">
/// The total said the way a person would say it: "142 pieces" reads as "1 box + 4 strips +
/// 2 pieces". Computed server-side through Module 2's converter so the API, the web pages and
/// any future mobile client all phrase it identically.
/// </param>
/// <param name="TotalQuantityInBaseUnits">
/// <b>Sellable</b> stock: expired batches are excluded from it and reported in
/// <paramref name="ExpiredQuantityInBaseUnits"/> instead.
///
/// <para>A judgement call, and worth stating. Counting expired stock in the headline total
/// would show a shelf of unsellable packs as healthy and keep the product off the reorder
/// list; leaving it out of the row altogether would lose the fact that there is something
/// physically there to dispose of. Reporting the two separately is the only version that is
/// true, and it is why the status badge can read "Out of stock" on a row whose expiry cell is
/// red with a quantity beside it.</para>
/// </param>
/// <param name="BatchCount">
/// Batches holding stock, expired ones included — they are on the shelf, and a count that
/// omitted them would disagree with the detail page.
/// </param>
public sealed record StockListItemDto(
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
    ExpiryState NearestExpiryState)
{
    /// <summary>Whether any of this product's stock has expired and is still on the shelf.</summary>
    public bool HasExpiredStock => ExpiredQuantityInBaseUnits > 0;
}

/// <summary>
/// How a product's stock stands against its reorder level.
///
/// <para>Computed server-side rather than left to each client to derive from two numbers:
/// three clients would eventually disagree about whether "equal to the reorder level" counts
/// as low. It does — that is what a reorder level means.</para>
/// </summary>
public enum StockStatus
{
    /// <summary>Above the reorder level.</summary>
    Ok = 0,

    /// <summary>At or below the reorder level, but not zero.</summary>
    Low = 1,

    /// <summary>No sellable stock at all.</summary>
    OutOfStock = 2,
}

/// <summary>Filters on the stock list. All of them default to "everything".</summary>
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

    /// <summary>Has a batch expiring within the alert window and not yet expired.</summary>
    ExpiringSoon = 1,

    /// <summary>Has at least one batch already past its expiry date.</summary>
    Expired = 2,
}

/// <summary>Which products the stock list covers.</summary>
public enum StockProductTypeFilter
{
    All = 0,
    Medicine = 1,
    Other = 2,
}

/// <summary>
/// One batch.
/// </summary>
/// <param name="PurchasePricePerBaseUnit">
/// Null for an Employee. The projection does not read the column for that role, so a cost
/// price they may not see never leaves the server — the absent column on screen is a
/// consequence, not the control.
/// </param>
/// <param name="ExpiryState">
/// Server-computed so that "expired" and "expiring soon" mean the same thing everywhere,
/// including in a future alerts module that will not be rendering a table.
/// </param>
public sealed record BatchDto(
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
    DateTime? ModifiedOnUtc)
{
    public bool IsDepleted => QuantityInBaseUnits <= 0;
}

/// <summary>Where a batch stands relative to its expiry date.</summary>
public enum ExpiryState
{
    /// <summary>No expiry date. Diapers, syringes, dressings.</summary>
    NotApplicable = 0,

    /// <summary>Beyond the alert window.</summary>
    Ok = 1,

    /// <summary>Within the alert window and not yet expired.</summary>
    ExpiringSoon = 2,

    /// <summary>Past its expiry date. Still on the shelf until someone removes it.</summary>
    Expired = 3,
}

/// <summary>
/// A product's stock in full: the summary a person reads first, then the batches.
/// </summary>
/// <param name="ActiveBatches">
/// Batches with stock left, <b>in FEFO order</b> — the batch to sell next is the first row.
/// Not paginated: a product holds a handful of live batches at a time.
/// </param>
/// <param name="DepletedBatches">
/// Sold-out batches, newest first, paginated because they accumulate for as long as the
/// pharmacy trades. Kept and shown rather than hidden: they are the cost and expiry behind
/// sales that already happened.
/// </param>
public sealed record ProductStockDto(
    Guid ProductId,
    string BrandName,
    string? GenericName,
    string? Strength,
    ProductType ProductType,
    bool IsAntibiotic,
    string BaseUnitName,
    string PackingSummary,
    decimal? PricePerBase,
    int ReorderLevel,
    int TotalQuantityInBaseUnits,
    string FormattedQuantity,
    int BatchCount,
    DateOnly? NearestExpiryDate,
    int? DaysUntilNearestExpiry,
    StockStatus Status,
    IReadOnlyList<BatchDto> ActiveBatches,
    int DepletedBatchCount,
    IReadOnlyList<BatchDto> DepletedBatches,
    int ExpiredQuantityInBaseUnits,
    string? FormattedExpiredQuantity,
    ExpiryState NearestExpiryState)
{
    public bool IsBelowReorderLevel => TotalQuantityInBaseUnits <= ReorderLevel;

    public bool HasExpiredStock => ExpiredQuantityInBaseUnits > 0;
}

/// <summary>One row of a batch's adjustment history.</summary>
/// <param name="AdjustedByName">
/// Resolved from the user id at read time rather than copied onto the row at write time, so a
/// corrected name is corrected everywhere. The id is the record; this is the display.
/// </param>
public sealed record StockAdjustmentDto(
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

/// <summary>
/// The result of creating a batch.
/// </summary>
/// <param name="SellsAtALoss">
/// True when the cost entered exceeds the product's own sale price. <b>A warning, not a
/// rejection</b> — a pharmacy really does sometimes buy above its list price and then reprice,
/// and refusing the entry would leave the stock unrecorded, which is worse than recording it
/// with a flag.
///
/// <para>The batch is saved by the time this is returned, so the confirmation the web form
/// shows happens <em>before</em> it posts, from the product price it already has on screen.
/// This flag is the authoritative server-side answer for every other caller — and the one
/// that still catches the case where the product was repriced between the form loading and
/// the post.</para>
/// </param>
public sealed record BatchCreatedDto(
    BatchDto Batch,
    bool SellsAtALoss,
    decimal? ProductPricePerBaseUnit,
    bool ProductIsSetupComplete,
    string? Warning);

/// <summary>
/// The result of an adjustment: the batch as it now stands, and the row that explains why.
///
/// <para>Both are returned because both were written, in one transaction, and a caller that
/// received only the new quantity would have no way to show what it recorded.</para>
/// </summary>
public sealed record BatchAdjustedDto(
    BatchDto Batch,
    StockAdjustmentDto Adjustment);
