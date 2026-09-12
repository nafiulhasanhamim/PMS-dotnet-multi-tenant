using PMS.Application.Common.DTOs;
using PMS.Domain.Enums;
using PMS.SharedKernel.Grid;

namespace PMS.Application.Interfaces;

/// <summary>
/// The reports. Aggregations over <c>Sale</c>, <c>SaleLine</c>, <c>SalesReturn</c>, <c>Batch</c>,
/// <c>Product</c> and <c>Users</c> — no entity of its own and no writes.
///
/// <para><b>Three rules run through every method here, and each fails silently on its own.</b>
/// Revenue is <c>NetLineTotal</c>; cost is the line's own <c>Batch.PurchasePricePerBaseUnit</c>;
/// a cancelled sale is excluded rather than zeroed. The arithmetic is in <c>ProfitMath</c>, which
/// the SQL below mirrors, and the reasoning is in <c>docs/08-reports-and-profit.md</c>.</para>
///
/// <para><b>Rounding happens at presentation, never here.</b> Aggregating rounded values drifts
/// by a paisa a line, which over a month is a discrepancy somebody will try to reconcile and
/// cannot.</para>
///
/// <para>No method takes a tenant id. Every entity read except <c>Users</c> is an
/// <c>ITenantEntity</c>, so the global query filter supplies the pharmacy — inside the aggregates
/// too, which is what makes one pharmacy's profit its own.</para>
/// </summary>
public interface IReportQueries
{
    /// <summary>
    /// One day: headline figures, every sale, and an hourly breakdown for the bar chart.
    ///
    /// <para><b>The day is bounded in UTC, consistently with every other date filter in the
    /// system</b> — the sales list, the antibiotic register, the alert windows. Introducing a
    /// second convention in one report would mean two screens disagreeing about which day a 2 a.m.
    /// sale belongs to, which is a worse problem than the one it would solve.</para>
    ///
    /// <para>The hourly buckets are therefore keyed by UTC hour, and the <em>page</em> shifts the
    /// labels into the display zone when it draws them — the same division of labour every
    /// timestamp in the application already uses. A properly local day boundary needs the
    /// per-tenant time zone that is already noted as deferred, and it should land everywhere at
    /// once rather than here alone.</para>
    /// </summary>
    Task<DailySalesReportDto> GetDailySalesReportAsync(
        DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// One month: headline figures including net profit, and a day-by-day breakdown.
    ///
    /// <para>Calls <c>IOperatingExpenses</c> for the expense figure rather than assuming zero, so
    /// Module 9 completes this report by filling in one method body.</para>
    /// </summary>
    Task<MonthlySalesReportDto> GetMonthlySalesReportAsync(
        int month, int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// What moved, ordered by quantity, revenue or profit.
    ///
    /// <para>Net of returns on all three measures: a product sold forty times and returned ten
    /// shows thirty, and the revenue and profit come down with it. A "top seller" list that
    /// counted returned goods would rank a product nobody kept above one they did.</para>
    /// </summary>
    Task<GridResult<TopSellingProductDto>> GetTopSellingProductsAsync(
        DateOnly from,
        DateOnly to,
        TopSellingSort sortBy,
        ProductType? productType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revenue and margin split by product type.
    ///
    /// <para>Newly worth having now that pharmacies stock non-medicines: it answers "how much of
    /// our margin comes from medicines versus baby care", which a single blended margin cannot.</para>
    /// </summary>
    Task<IReadOnlyList<SalesByProductTypeRowDto>> GetSalesByProductTypeAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Products holding stock that has not sold within <paramref name="thresholdDays"/>, or has
    /// never sold at all.
    ///
    /// <para><b>The outer join is the whole report.</b> A product with no sale lines has no rows
    /// to join to, and an inner join drops it — silently removing exactly the never-sold products
    /// that are the deadest stock on the shelf and the most important rows here. The bug leaves
    /// a report that looks plausible and omits its own headline cases.</para>
    ///
    /// <para>Sorted by days since last sale descending, never-sold first.</para>
    /// </summary>
    Task<GridResult<DeadStockRowDto>> GetDeadStockAsync(
        int thresholdDays,
        ProductType? productType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Totals for the whole dead-stock set, including the capital tied up in it.</summary>
    Task<DeadStockSummaryDto> GetDeadStockSummaryAsync(
        int thresholdDays,
        ProductType? productType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Per cashier: what they rang up and what they discounted.
    ///
    /// <para>Counter accountability. The average discount column is the one worth reading — a
    /// cashier whose discounts run well above everyone else's is a question, not yet an
    /// accusation, and the report is written to be read that way.</para>
    /// </summary>
    Task<IReadOnlyList<SalesPerUserRowDto>> GetSalesPerUserAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>
    /// What the current inventory is worth at cost. A snapshot — no date range, because there is
    /// no history of stock levels to look back through.
    ///
    /// <para><b>Expired stock is excluded from the value and reported separately.</b> It is not
    /// worth its cost price; it is a write-off waiting to happen. Folding it in would overstate
    /// the one figure an owner reads as an asset.</para>
    /// </summary>
    Task<GridResult<StockValuationRowDto>> GetStockValuationAsync(
        ProductType? productType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Totals for the whole valuation, including the separate expired figure.</summary>
    Task<StockValuationSummaryDto> GetStockValuationSummaryAsync(
        ProductType? productType, CancellationToken cancellationToken = default);

    // ── Streamed exports ─────────────────────────────────────────────────────────────────
    //
    // The paginated reports export their whole filtered set rather than one page, streamed for
    // the same reason Module 7's register is: an export is precisely the request that asks for
    // everything at once, and buffering it to write it straight out again holds a year in memory
    // for no benefit. The unpaginated reports are bounded by their own shape - a day has one
    // day's sales, a month has 31 rows - so they export from the same method the page uses.

    IAsyncEnumerable<TopSellingProductDto> StreamTopSellingProductsAsync(
        DateOnly from,
        DateOnly to,
        TopSellingSort sortBy,
        ProductType? productType,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<DeadStockRowDto> StreamDeadStockAsync(
        int thresholdDays,
        ProductType? productType,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<StockValuationRowDto> StreamStockValuationAsync(
        ProductType? productType, CancellationToken cancellationToken = default);
}
