namespace PMS.Web.Api;

// ═══════════════════════════════════════════════════════════════════════════════════════════
// Module 8 wire contracts.
//
// Enum values must match the server's NUMERIC values — the API serialises enums as integers.
//
// Every money figure arrives UNROUNDED, because the API aggregates unrounded and rounds once at
// the edge. That edge is here: Money.Format in Presentation/Money.cs. Nothing in this file
// rounds, and no page should either.
// ═══════════════════════════════════════════════════════════════════════════════════════════

/// <summary>How the top-selling report is ordered.</summary>
public enum TopSellingSort
{
    Quantity = 0,
    Revenue = 1,
    Profit = 2,
}

// ── Daily ────────────────────────────────────────────────────────────────────────────────

public sealed record DailySaleRow(
    Guid SaleId,
    string InvoiceNumber,
    DateTime SaleDate,
    string? CashierName,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal NetTotal,
    decimal Cost,
    decimal Profit);

/// <param name="Hour">0–23 in UTC. The page shifts it into the display zone when it draws it.</param>
public sealed record HourlySales(int Hour, int TransactionCount, decimal Sales);

public sealed record DailySalesReport(
    DateOnly Date,
    decimal TotalSales,
    int TransactionCount,
    decimal TotalDiscount,
    decimal GrossProfit,
    decimal TotalRefunded,
    int ReturnCount,
    decimal ReturnImpact,
    IReadOnlyList<DailySaleRow> Sales,
    IReadOnlyList<HourlySales> Hourly)
{
    public static DailySalesReport Empty { get; } =
        new(default, 0, 0, 0, 0, 0, 0, 0, [], []);

    /// <summary>Gross profit as a share of sales. Zero sales gives zero, not a divide by zero.</summary>
    public decimal MarginPercent =>
        TotalSales == 0m ? 0m : GrossProfit / TotalSales * 100m;

    /// <summary>What an average customer spent. The denominator excludes cancelled sales.</summary>
    public decimal AverageBasket =>
        TransactionCount == 0 ? 0m : TotalSales / TransactionCount;
}

// ── Monthly ──────────────────────────────────────────────────────────────────────────────

public sealed record DailyBreakdownRow(
    DateOnly Date, int TransactionCount, decimal Sales, decimal Profit);

public sealed record MonthlySalesReport(
    int Month,
    int Year,
    decimal TotalSales,
    int TransactionCount,
    decimal TotalDiscount,
    decimal GrossProfit,
    decimal OperatingExpenses,
    decimal NetProfit,
    decimal TotalRefunded,
    decimal ReturnImpact,
    IReadOnlyList<DailyBreakdownRow> Days)
{
    public static MonthlySalesReport Empty { get; } =
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, []);

    public decimal MarginPercent =>
        TotalSales == 0m ? 0m : GrossProfit / TotalSales * 100m;

    /// <summary>
    /// Whether the net-profit figure is still missing its expenses.
    ///
    /// <para>Drives the caveat beside it. Expressed as "no expenses have been recorded" rather
    /// than hard-coded to false, so that the day Module 9 lands and a pharmacy records its first
    /// salary, the notice disappears on its own — and a month genuinely without expenses still
    /// says so, which is also true.</para>
    /// </summary>
    public bool ExpensesMissing => OperatingExpenses == 0m;

    public string MonthName =>
        Month is >= 1 and <= 12
            ? new DateOnly(Year, Month, 1).ToString("MMMM yyyy")
            : $"{Month}/{Year}";
}

// ── Top selling ──────────────────────────────────────────────────────────────────────────

public sealed record TopSellingProduct(
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

public sealed record SalesByProductTypeRow(
    ProductType ProductType,
    int LineCount,
    int QuantityInBaseUnits,
    decimal Revenue,
    decimal Cost,
    decimal Profit,
    decimal MarginPercent);

// ── Dead stock ───────────────────────────────────────────────────────────────────────────

public sealed record DeadStockRow(
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

public sealed record DeadStockSummary(
    int ThresholdDays, int ProductCount, int NeverSoldCount, decimal TotalCapitalTiedUp);

public sealed record DeadStockPage(
    int ThresholdDays, ApiPage<DeadStockRow> Rows, DeadStockSummary Summary)
{
    public static DeadStockPage Empty { get; } = new(
        90, ApiPage<DeadStockRow>.Empty, new DeadStockSummary(90, 0, 0, 0));
}

// ── Sales per user ───────────────────────────────────────────────────────────────────────

public sealed record SalesPerUserRow(
    Guid UserId,
    string Name,
    UserRole? Role,
    int TransactionCount,
    decimal TotalSales,
    decimal TotalDiscount,
    decimal AverageDiscountPercent);

// ── Stock valuation ──────────────────────────────────────────────────────────────────────

public sealed record StockValuationRow(
    Guid ProductId,
    string BrandName,
    string? GenericName,
    ProductType ProductType,
    int QuantityInBaseUnits,
    string FormattedQuantity,
    decimal WeightedAverageCost,
    decimal TotalValueAtCost,
    int ExpiredQuantityInBaseUnits,
    decimal ExpiredValueAtCost)
{
    public bool HasExpired => ExpiredQuantityInBaseUnits > 0;
}

public sealed record StockValuationSummary(
    int ProductCount,
    int TotalQuantityInBaseUnits,
    decimal TotalValueAtCost,
    int ExpiredQuantityInBaseUnits,
    decimal ExpiredValueAtCost);

public sealed record StockValuationPage(
    ApiPage<StockValuationRow> Rows, StockValuationSummary Summary)
{
    public static StockValuationPage Empty { get; } = new(
        ApiPage<StockValuationRow>.Empty, new StockValuationSummary(0, 0, 0, 0, 0));
}
