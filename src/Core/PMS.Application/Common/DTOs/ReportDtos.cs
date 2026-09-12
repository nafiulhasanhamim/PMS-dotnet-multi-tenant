using PMS.Domain.Enums;
using PMS.SharedKernel.Grid;

namespace PMS.Application.Common.DTOs;

// ── Daily ────────────────────────────────────────────────────────────────────────────────

/// <summary>One completed sale on the daily report.</summary>
/// <param name="Profit">
/// Net of the line's share of the bill discount, and after the cost of the specific batches each
/// line came out of. See <c>ProfitMath</c>.
/// </param>
public sealed record DailySaleRowDto(
    Guid SaleId,
    string InvoiceNumber,
    DateTime SaleDate,
    string? CashierName,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal NetTotal,
    decimal Cost,
    decimal Profit);

/// <summary>Sales in one hour of the day, for the bar chart.</summary>
/// <param name="Hour">0–23, in the pharmacy's display zone rather than UTC — see the query.</param>
public sealed record HourlySalesDto(int Hour, int TransactionCount, decimal Sales);

public sealed record DailySalesReportDto(
    DateOnly Date,
    decimal TotalSales,
    int TransactionCount,
    decimal TotalDiscount,
    decimal GrossProfit,
    decimal TotalRefunded,
    int ReturnCount,

    /// <summary>
    /// What returns took out of the day's profit: refunds less the cost of the stock that came
    /// back. Already subtracted from <see cref="GrossProfit"/>; reported separately so the figure
    /// can be explained rather than just absorbed.
    /// </summary>
    decimal ReturnImpact,
    IReadOnlyList<DailySaleRowDto> Sales,
    IReadOnlyList<HourlySalesDto> Hourly);

// ── Monthly ──────────────────────────────────────────────────────────────────────────────

public sealed record DailyBreakdownRowDto(
    DateOnly Date,
    int TransactionCount,
    decimal Sales,
    decimal Profit);

public sealed record MonthlySalesReportDto(
    int Month,
    int Year,
    decimal TotalSales,
    int TransactionCount,
    decimal TotalDiscount,
    decimal GrossProfit,

    /// <summary>
    /// Zero until Module 9 fills in <c>IOperatingExpenses</c>. The report says so on screen
    /// rather than presenting gross profit under a heading that reads "net".
    /// </summary>
    decimal OperatingExpenses,
    decimal NetProfit,
    decimal TotalRefunded,
    decimal ReturnImpact,
    IReadOnlyList<DailyBreakdownRowDto> Days);

// ── Top selling ──────────────────────────────────────────────────────────────────────────

/// <summary>How the top-selling report is ordered.</summary>
public enum TopSellingSort
{
    Quantity = 0,
    Revenue = 1,
    Profit = 2,
}

/// <param name="QuantitySoldInBaseUnits">
/// Net of returns. A product sold forty times and returned ten shows thirty, because that is what
/// left the pharmacy.
/// </param>
public sealed record TopSellingProductDto(
    Guid ProductId,
    string BrandName,
    string? GenericName,
    ProductType ProductType,
    int QuantitySoldInBaseUnits,
    string FormattedQuantity,
    decimal Revenue,
    decimal Profit,
    decimal MarginPercent);

// ── By product type ──────────────────────────────────────────────────────────────────────

public sealed record SalesByProductTypeRowDto(
    ProductType ProductType,
    int LineCount,
    int QuantityInBaseUnits,
    decimal Revenue,
    decimal Cost,
    decimal Profit,
    decimal MarginPercent);

// ── Dead stock ───────────────────────────────────────────────────────────────────────────

/// <param name="LastSoldOn">
/// Null when the product has never been sold. Those are the deadest rows on the report and the
/// reason the query needs an outer join — see the module doc.
/// </param>
/// <param name="DaysSinceLastSale">
/// Null alongside a null <paramref name="LastSoldOn"/>. Never-sold rows sort first.
/// </param>
public sealed record DeadStockRowDto(
    Guid ProductId,
    string BrandName,
    string? GenericName,
    ProductType ProductType,
    int QuantityInBaseUnits,
    string FormattedQuantity,
    DateOnly? LastSoldOn,
    int? DaysSinceLastSale,
    decimal StockValueAtCost)
{
    public bool NeverSold => LastSoldOn is null;
}

/// <param name="TotalCapitalTiedUp">
/// Across the whole filtered set, not the page. This is the number that motivates somebody to
/// act, so it has to describe the report rather than whatever happens to be on screen.
/// </param>
public sealed record DeadStockSummaryDto(
    int ThresholdDays,
    int ProductCount,
    int NeverSoldCount,
    decimal TotalCapitalTiedUp);

// ── Sales per user ───────────────────────────────────────────────────────────────────────

/// <param name="AverageDiscountPercent">
/// Discount given as a share of the subtotals this cashier rang up. The point of the report:
/// spotting someone whose discounts run well above everyone else's.
/// </param>
public sealed record SalesPerUserRowDto(
    Guid UserId,
    string Name,
    UserRole? Role,
    int TransactionCount,
    decimal TotalSales,
    decimal TotalDiscount,
    decimal AverageDiscountPercent);

// ── Stock valuation ──────────────────────────────────────────────────────────────────────

public sealed record StockValuationRowDto(
    Guid ProductId,
    string BrandName,
    string? GenericName,
    ProductType ProductType,
    int QuantityInBaseUnits,
    string FormattedQuantity,
    decimal WeightedAverageCost,
    decimal TotalValueAtCost,
    int ExpiredQuantityInBaseUnits,
    decimal ExpiredValueAtCost);

/// <param name="ExpiredValueAtCost">
/// Reported separately and excluded from <paramref name="TotalValueAtCost"/>. Expired stock is
/// not worth its cost price — it is a write-off waiting to happen — so folding it into "money
/// tied up in inventory" would overstate the one figure an owner reads as an asset. See the
/// module doc.
/// </param>
public sealed record StockValuationSummaryDto(
    int ProductCount,
    int TotalQuantityInBaseUnits,
    decimal TotalValueAtCost,
    int ExpiredQuantityInBaseUnits,
    decimal ExpiredValueAtCost);

// ── Page envelopes ───────────────────────────────────────────────────────────────────────
//
// The two paginated reports that carry a summary return both in one response. A page that
// fetched its rows and its headline totals separately could render a header that disagrees with
// the table beneath it - and on a figure like "45,000 tied up in dead stock", which is the number
// somebody acts on, that disagreement is worse than having no header.

/// <param name="ThresholdDays">
/// The threshold actually used, after coercion - not necessarily the one asked for. The page
/// echoes this back so the filter control reflects what was applied rather than what was typed.
/// </param>
public sealed record DeadStockPageDto(
    int ThresholdDays,
    GridResult<DeadStockRowDto> Rows,
    DeadStockSummaryDto Summary);

public sealed record StockValuationPageDto(
    GridResult<StockValuationRowDto> Rows,
    StockValuationSummaryDto Summary);
