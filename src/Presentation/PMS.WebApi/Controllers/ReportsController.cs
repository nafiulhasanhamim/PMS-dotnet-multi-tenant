using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Reports;
using PMS.Application.Common.Stock;
using PMS.Application.Features.Reports.Queries.GetDailySalesReport;
using PMS.Application.Features.Reports.Queries.GetDeadStock;
using PMS.Application.Features.Reports.Queries.GetMonthlySalesReport;
using PMS.Application.Features.Reports.Queries.GetSalesByProductType;
using PMS.Application.Features.Reports.Queries.GetSalesPerUser;
using PMS.Application.Features.Reports.Queries.GetStockValuation;
using PMS.Application.Features.Reports.Queries.GetSupplierDues;
using PMS.Application.Features.Reports.Queries.GetTopSellingProducts;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Interfaces;
using PMS.WebApi.Csv;
using PMS.WebApi.Extensions;

namespace PMS.WebApi.Controllers;

/// <summary>
/// Reports and profit.
///
/// <para><b>Admin only, every action, and this is the strictest area in the application.</b> Other
/// modules distinguish reading from writing; here the reading <em>is</em> the sensitive act. These
/// endpoints expose purchase costs, margins, what the business earns, and a per-cashier discount
/// breakdown that is in effect a staff performance review. A pharmacist who may dispense a
/// controlled drug and adjust stock still has no business knowing the owner's margin on it, and a
/// cashier has no business reading a table ranking their colleagues' discounting.</para>
///
/// <para>So: <c>TenantAdminPolicy</c>, not <c>TenantWriterPolicy</c>. There is no read-only
/// variant of this controller and no per-report relaxation, because every report here joins to
/// batch cost.</para>
///
/// <para>No action takes a tenant id. The queries carry <c>ITenantScopedRequest</c> and the global
/// query filter supplies the pharmacy — inside the aggregates too, which is what makes one
/// pharmacy's profit its own.</para>
/// </summary>
[Route("api/reports")]
[Authorize(Policy = AuthenticationExtensions.TenantAdminPolicy)]
public class ReportsController : ApiControllerBase
{
    private readonly IReportQueries _reports;
    private readonly ITenantSettings _settings;
    private readonly IDateTime _clock;

    public ReportsController(
        IReportQueries reports, ITenantSettings settings, IDateTime clock)
    {
        _reports = reports;
        _settings = settings;
        _clock = clock;
    }

    // ── Daily ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One day: totals, gross profit, every sale, and an hourly breakdown.
    ///
    /// <para>Defaults to today. Cancelled sales are absent rather than zeroed — see
    /// <c>ProfitMath</c> for why that distinction is not cosmetic.</para>
    /// </summary>
    [HttpGet("daily-sales")]
    [ProducesResponseType(typeof(DailySalesReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDailySales(
        [FromQuery] DateOnly? date = null,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetDailySalesReportQuery(date), cancellationToken));

    /// <summary>The day's sales as CSV. Bounded by the day, so it exports in one go.</summary>
    [HttpGet("daily-sales/export")]
    [Produces("text/csv")]
    public async Task ExportDailySales(
        [FromQuery] DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        var day = date ?? _clock.UtcDateToday();
        var report = await _reports.GetDailySalesReportAsync(day, cancellationToken);

        await using var writer = await BeginCsvAsync(
            $"daily-sales-{day:yyyy-MM-dd}",
            "Daily sales report",
            $"Date,{day:yyyy-MM-dd}",
            cancellationToken);

        await writer.WriteLineAsync(
            "Time,Invoice,Cashier,Subtotal,Discount,Net total,Cost,Profit");

        foreach (var row in report.Sales)
        {
            await writer.WriteLineAsync(string.Join(',',
                CsvField.Timestamp(row.SaleDate),
                CsvField.Text(row.InvoiceNumber),
                CsvField.Text(row.CashierName),
                CsvField.Money(row.Subtotal),
                CsvField.Money(row.DiscountAmount),
                CsvField.Money(row.NetTotal),
                CsvField.Money(row.Cost),
                CsvField.Money(row.Profit)));
        }

        await writer.WriteLineAsync();
        await writer.WriteLineAsync($"Total sales,{CsvField.Money(report.TotalSales)}");
        await writer.WriteLineAsync($"Transactions,{CsvField.Number(report.TransactionCount)}");
        await writer.WriteLineAsync($"Total discount,{CsvField.Money(report.TotalDiscount)}");
        await writer.WriteLineAsync($"Gross profit,{CsvField.Money(report.GrossProfit)}");
        await writer.WriteLineAsync($"Refunded,{CsvField.Money(report.TotalRefunded)}");
        await writer.WriteLineAsync($"Return impact on profit,{CsvField.Money(report.ReturnImpact)}");

        await writer.FlushAsync(cancellationToken);
    }

    // ── Monthly ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One month: totals, gross profit, net profit, and a day-by-day breakdown.
    ///
    /// <para><b>Net profit equals gross profit today</b>, because operating expenses come from the
    /// Salary module and that is Module 9. The figure is fetched through <c>IOperatingExpenses</c>
    /// rather than assumed, and the page says on screen that salaries are not yet in it.</para>
    /// </summary>
    [HttpGet("monthly-sales")]
    [ProducesResponseType(typeof(MonthlySalesReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMonthlySales(
        [FromQuery] int? month = null,
        [FromQuery] int? year = null,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetMonthlySalesReportQuery(month, year), cancellationToken));

    /// <summary>The month day by day, as CSV. At most 31 rows — no streaming needed.</summary>
    [HttpGet("monthly-sales/export")]
    [Produces("text/csv")]
    public async Task ExportMonthlySales(
        [FromQuery] int? month = null,
        [FromQuery] int? year = null,
        CancellationToken cancellationToken = default)
    {
        var today = _clock.UtcDateToday();
        var m = month ?? today.Month;
        var y = year ?? today.Year;

        // Guarded here as well as in the validator: this action bypasses MediatR to stream, so
        // the pipeline's validator never runs for it, and new DateOnly(y, 13, 1) would throw.
        if (m is < 1 or > 12 || y is < 2000 or > 2100)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var report = await _reports.GetMonthlySalesReportAsync(m, y, cancellationToken);

        await using var writer = await BeginCsvAsync(
            $"monthly-sales-{y:0000}-{m:00}",
            "Monthly sales report",
            $"Month,{y:0000}-{m:00}",
            cancellationToken);

        await writer.WriteLineAsync("Date,Transactions,Sales,Profit");

        foreach (var row in report.Days)
        {
            await writer.WriteLineAsync(string.Join(',',
                CsvField.Date(row.Date),
                CsvField.Number(row.TransactionCount),
                CsvField.Money(row.Sales),
                CsvField.Money(row.Profit)));
        }

        await writer.WriteLineAsync();
        await writer.WriteLineAsync($"Total sales,{CsvField.Money(report.TotalSales)}");
        await writer.WriteLineAsync($"Transactions,{CsvField.Number(report.TransactionCount)}");
        await writer.WriteLineAsync($"Total discount,{CsvField.Money(report.TotalDiscount)}");
        await writer.WriteLineAsync($"Gross profit,{CsvField.Money(report.GrossProfit)}");
        await writer.WriteLineAsync(
            $"Operating expenses,{CsvField.Money(report.OperatingExpenses)}");
        await writer.WriteLineAsync($"Net profit,{CsvField.Money(report.NetProfit)}");

        // The caveat travels with the file. An exported figure headed "net profit" that silently
        // omits the largest recurring cost a pharmacy has is the one thing this module must not
        // hand somebody.
        await writer.WriteLineAsync();
        await writer.WriteLineAsync(CsvField.Text(
            "Note: operating expenses are not yet recorded (the salary module is not built), so "
            + "net profit above equals gross profit and does not include staff costs."));

        await writer.FlushAsync(cancellationToken);
    }

    // ── Top selling ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// What moved, net of returns, ordered by quantity, revenue or profit.
    ///
    /// <para>Dates default to the last thirty days. Returns are subtracted from all three
    /// measures: a product sold forty times and returned ten shows thirty.</para>
    /// </summary>
    [HttpGet("top-selling")]
    [ProducesResponseType(typeof(GridResultOfTopSelling), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopSelling(
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        [FromQuery] TopSellingSort sortBy = TopSellingSort.Quantity,
        [FromQuery] ProductType? productType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AlertPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetTopSellingProductsQuery(
                dateFrom, dateTo, sortBy, productType, page, pageSize),
            cancellationToken));

    /// <summary>
    /// The whole filtered set as CSV — not one page. Streamed, because a year of a pharmacy's
    /// catalogue is a list nobody should have to hold in memory to copy it straight out again.
    /// </summary>
    [HttpGet("top-selling/export")]
    [Produces("text/csv")]
    public async Task ExportTopSelling(
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        [FromQuery] TopSellingSort sortBy = TopSellingSort.Quantity,
        [FromQuery] ProductType? productType = null,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ReportRange.Resolve(dateFrom, dateTo, _clock.UtcDateToday());

        await using var writer = await BeginCsvAsync(
            $"top-selling-{from:yyyy-MM-dd}-to-{to:yyyy-MM-dd}",
            "Top selling products",
            $"Date range,{from:yyyy-MM-dd} to {to:yyyy-MM-dd}",
            cancellationToken);

        await writer.WriteLineAsync(
            "Product,Generic,Type,Quantity,Quantity (base units),Revenue,Profit,Margin %");

        var rows = _reports.StreamTopSellingProductsAsync(
            from, to, sortBy, productType, cancellationToken);

        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            await writer.WriteLineAsync(string.Join(',',
                CsvField.Text(row.BrandName),
                CsvField.Text(row.GenericName),
                CsvField.Text(row.ProductType.ToString()),
                CsvField.Text(row.FormattedQuantity),
                CsvField.Number(row.QuantitySoldInBaseUnits),
                CsvField.Money(row.Revenue),
                CsvField.Money(row.Profit),
                CsvField.Percent(row.MarginPercent)));
        }

        await writer.FlushAsync(cancellationToken);
    }

    // ── By product type ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Revenue and margin split by product type — how much of the margin comes from medicines
    /// versus everything else, which a single blended figure cannot answer.
    /// </summary>
    [HttpGet("sales-by-product-type")]
    [ProducesResponseType(typeof(IReadOnlyList<SalesByProductTypeRowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalesByProductType(
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetSalesByProductTypeQuery(dateFrom, dateTo), cancellationToken));

    /// <summary>One row per product type. Bounded by the enum — no streaming needed.</summary>
    [HttpGet("sales-by-product-type/export")]
    [Produces("text/csv")]
    public async Task ExportSalesByProductType(
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ReportRange.Resolve(dateFrom, dateTo, _clock.UtcDateToday());
        var rows = await _reports.GetSalesByProductTypeAsync(from, to, cancellationToken);

        await using var writer = await BeginCsvAsync(
            $"sales-by-product-type-{from:yyyy-MM-dd}-to-{to:yyyy-MM-dd}",
            "Sales by product type",
            $"Date range,{from:yyyy-MM-dd} to {to:yyyy-MM-dd}",
            cancellationToken);

        await writer.WriteLineAsync("Type,Lines,Quantity (base units),Revenue,Cost,Profit,Margin %");

        foreach (var row in rows)
        {
            await writer.WriteLineAsync(string.Join(',',
                CsvField.Text(row.ProductType.ToString()),
                CsvField.Number(row.LineCount),
                CsvField.Number(row.QuantityInBaseUnits),
                CsvField.Money(row.Revenue),
                CsvField.Money(row.Cost),
                CsvField.Money(row.Profit),
                CsvField.Percent(row.MarginPercent)));
        }

        await writer.FlushAsync(cancellationToken);
    }

    // ── Dead stock ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Stock that has not sold within the threshold, or has never sold at all, with the capital
    /// tied up in it.
    ///
    /// <para><b>Never-sold products are the point of the report</b> and sort first. See the module
    /// doc for why the query cannot use an inner join.</para>
    /// </summary>
    [HttpGet("dead-stock")]
    [ProducesResponseType(typeof(DeadStockPageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDeadStock(
        [FromQuery] int? thresholdDays = null,
        [FromQuery] ProductType? productType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AlertPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetDeadStockQuery(thresholdDays, productType, page, pageSize),
            cancellationToken));

    /// <summary>The whole dead-stock set as CSV, streamed.</summary>
    [HttpGet("dead-stock/export")]
    [Produces("text/csv")]
    public async Task ExportDeadStock(
        [FromQuery] int? thresholdDays = null,
        [FromQuery] ProductType? productType = null,
        CancellationToken cancellationToken = default)
    {
        var threshold = StockPolicy.CoerceDeadStockThreshold(thresholdDays);

        await using var writer = await BeginCsvAsync(
            $"dead-stock-{threshold}-days",
            "Dead stock report",
            $"Not sold in,{threshold} days",
            cancellationToken);

        await writer.WriteLineAsync(
            "Product,Generic,Type,Quantity,Quantity (base units),Last sold,"
            + "Days since last sale,Value at cost");

        var rows = _reports.StreamDeadStockAsync(threshold, productType, cancellationToken);

        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            await writer.WriteLineAsync(string.Join(',',
                CsvField.Text(row.BrandName),
                CsvField.Text(row.GenericName),
                CsvField.Text(row.ProductType.ToString()),
                CsvField.Text(row.FormattedQuantity),
                CsvField.Number(row.QuantityInBaseUnits),

                // Spelled out rather than left blank. An empty cell in a date column reads as
                // missing data; "Never" is the finding.
                row.NeverSold ? "Never" : CsvField.Date(row.LastSoldOn),
                row.DaysSinceLastSale is { } days ? CsvField.Number(days) : string.Empty,
                CsvField.Money(row.StockValueAtCost)));
        }

        await writer.FlushAsync(cancellationToken);
    }

    // ── Sales per user ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Per cashier: transactions, sales and discount given.
    ///
    /// <para>The average-discount column is what this is read for. Admin-only for an obvious
    /// reason — it is a staff comparison.</para>
    /// </summary>
    [HttpGet("sales-per-user")]
    [ProducesResponseType(typeof(IReadOnlyList<SalesPerUserRowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalesPerUser(
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetSalesPerUserQuery(dateFrom, dateTo), cancellationToken));

    /// <summary>One row per cashier. Bounded by headcount — no streaming needed.</summary>
    [HttpGet("sales-per-user/export")]
    [Produces("text/csv")]
    public async Task ExportSalesPerUser(
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ReportRange.Resolve(dateFrom, dateTo, _clock.UtcDateToday());
        var rows = await _reports.GetSalesPerUserAsync(from, to, cancellationToken);

        await using var writer = await BeginCsvAsync(
            $"sales-per-user-{from:yyyy-MM-dd}-to-{to:yyyy-MM-dd}",
            "Sales per user",
            $"Date range,{from:yyyy-MM-dd} to {to:yyyy-MM-dd}",
            cancellationToken);

        await writer.WriteLineAsync(
            "User,Role,Transactions,Total sales,Total discount,Average discount %");

        foreach (var row in rows)
        {
            await writer.WriteLineAsync(string.Join(',',
                CsvField.Text(row.Name),
                CsvField.Text(row.Role?.ToString()),
                CsvField.Number(row.TransactionCount),
                CsvField.Money(row.TotalSales),
                CsvField.Money(row.TotalDiscount),
                CsvField.Percent(row.AverageDiscountPercent)));
        }

        await writer.FlushAsync(cancellationToken);
    }

    // ── Stock valuation ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// What the shelves are worth at cost, right now.
    ///
    /// <para>A snapshot, with no date range: there is no history of stock levels, so "as at last
    /// month" is a question this system honestly cannot answer. <b>Expired stock is counted
    /// separately and left out of the value</b> — it cannot be sold, so calling it an asset would
    /// overstate the one figure an owner reads as money tied up.</para>
    /// </summary>
    [HttpGet("stock-valuation")]
    [ProducesResponseType(typeof(StockValuationPageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStockValuation(
        [FromQuery] ProductType? productType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = AlertPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetStockValuationQuery(productType, page, pageSize), cancellationToken));

    /// <summary>The whole valuation as CSV, streamed.</summary>
    [HttpGet("stock-valuation/export")]
    [Produces("text/csv")]
    public async Task ExportStockValuation(
        [FromQuery] ProductType? productType = null,
        CancellationToken cancellationToken = default)
    {
        await using var writer = await BeginCsvAsync(
            $"stock-valuation-{_clock.UtcDateToday():yyyy-MM-dd}",
            "Stock valuation at cost",
            $"As at,{_clock.UtcDateToday():yyyy-MM-dd}",
            cancellationToken);

        await writer.WriteLineAsync(
            "Product,Generic,Type,Quantity,Quantity (base units),Average cost per base unit,"
            + "Value at cost,Expired quantity (base units),Expired value at cost");

        var rows = _reports.StreamStockValuationAsync(productType, cancellationToken);

        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            await writer.WriteLineAsync(string.Join(',',
                CsvField.Text(row.BrandName),
                CsvField.Text(row.GenericName),
                CsvField.Text(row.ProductType.ToString()),
                CsvField.Text(row.FormattedQuantity),
                CsvField.Number(row.QuantityInBaseUnits),

                // Four places, not two: a per-base-unit cost derived from a bulk pack is
                // frequently fractional, and rounding it to paisa here would not reproduce the
                // value column beside it.
                row.WeightedAverageCost.ToString(
                    "0.0000", System.Globalization.CultureInfo.InvariantCulture),
                CsvField.Money(row.TotalValueAtCost),
                CsvField.Number(row.ExpiredQuantityInBaseUnits),
                CsvField.Money(row.ExpiredValueAtCost)));
        }

        await writer.FlushAsync(cancellationToken);
    }

    // ── Supplier dues (Module 4 retrofit) ────────────────────────────────────────────────

    /// <summary>
    /// What each supplier is owed: purchased, paid, returned and outstanding.
    ///
    /// <para><b>This report shipped disabled with Module 8</b> and is completed here, because it
    /// needs purchases and supplier payments — which did not exist until Module 4. It was left as
    /// a visibly disabled card rather than a page returning zeros, on the grounds that a money
    /// report confidently saying "0.00 owed" is one somebody would pay a supplier on.</para>
    ///
    /// <para><b>Defaults to all time</b>, unlike every other report here. "Who do we owe" has no
    /// date range; supplying one narrows the purchased, paid and returned columns while the
    /// outstanding column continues to cover everything, because a debt is a fact about now.</para>
    ///
    /// <para>The outstanding figure is read from <c>ISupplierBalanceQueries</c> — the same service
    /// the supplier detail page calls — so the two cannot disagree.</para>
    /// </summary>
    [HttpGet("supplier-dues")]
    [ProducesResponseType(typeof(SupplierDuesReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSupplierDues(
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        [FromQuery] bool allTime = true,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new GetSupplierDuesQuery(dateFrom, dateTo, allTime), cancellationToken));

    /// <summary>
    /// The dues report as CSV. One row per supplier — bounded by how many a pharmacy buys from,
    /// so it exports from the same method the page uses rather than streaming.
    /// </summary>
    [HttpGet("supplier-dues/export")]
    [Produces("text/csv")]
    public async Task ExportSupplierDues(
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        [FromQuery] bool allTime = true,
        CancellationToken cancellationToken = default)
    {
        var report = await _reports.GetSupplierDuesAsync(
            allTime ? null : dateFrom, allTime ? null : dateTo, allTime, cancellationToken);

        await using var writer = await BeginCsvAsync(
            "supplier-dues-" + (report.AllTime
                ? $"all-time-{_clock.UtcDateToday():yyyy-MM-dd}"
                : $"{report.From:yyyy-MM-dd}-to-{report.To:yyyy-MM-dd}"),
            "Supplier dues",
            report.AllTime
                ? "Period,All time"
                : $"Period,{report.From:yyyy-MM-dd} to {report.To:yyyy-MM-dd}",
            cancellationToken);

        await writer.WriteLineAsync(
            "Supplier,Company,Phone,Status,Purchased,Paid,Returned,Outstanding");

        foreach (var row in report.Rows)
        {
            await writer.WriteLineAsync(string.Join(',',
                CsvField.Text(row.SupplierName),
                CsvField.Text(row.Company),
                CsvField.Text(row.Phone),
                row.IsActive ? "Active" : "Inactive",
                CsvField.Money(row.PeriodActivity.TotalPurchased),
                CsvField.Money(row.PeriodActivity.TotalPaid),
                CsvField.Money(row.PeriodActivity.TotalReturned),
                CsvField.Money(row.Balance.Outstanding)));
        }

        await writer.WriteLineAsync();
        await writer.WriteLineAsync(string.Join(',',
            "Total", string.Empty, string.Empty, string.Empty,
            CsvField.Money(report.TotalPurchased),
            CsvField.Money(report.TotalPaid),
            CsvField.Money(report.TotalReturned),
            CsvField.Money(report.TotalOutstanding)));

        if (!report.AllTime)
        {
            // The caveat travels with the file, because a CSV that has been emailed on is read
            // without the screen that explained it.
            await writer.WriteLineAsync();
            await writer.WriteLineAsync(CsvField.Text(
                "Note: purchased, paid and returned cover the selected period. Outstanding is "
                + "the full amount owed as at today, which is what is actually payable."));
        }

        await writer.FlushAsync(cancellationToken);
    }

    // ── CSV plumbing ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the response up as a CSV download and writes the header block every report shares.
    ///
    /// <para>The block names the pharmacy, the period and when the file was taken, so an export
    /// that has been emailed on is still self-describing. Reports get compared against each other
    /// and against a till; a file that cannot say which pharmacy and which days it covers is how
    /// two people end up arguing about different numbers.</para>
    ///
    /// <para>Written straight to <c>Response.Body</c>. Building a string or a <c>MemoryStream</c>
    /// and returning a <c>FileResult</c> would buffer the whole export to hand it back unchanged,
    /// which is the one thing the streaming exports exist to avoid.</para>
    /// </summary>
    private async Task<StreamWriter> BeginCsvAsync(
        string fileName, string title, string periodLine, CancellationToken cancellationToken)
    {
        var pharmacy = await _settings.GetPharmacyNameAsync(cancellationToken);

        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}.csv\"";

        // The BOM is not decoration: Excel reads a UTF-8 CSV without one as the system code page,
        // and a pharmacy name in Bangla comes out as mojibake.
        var writer = new StreamWriter(Response.Body, new UTF8Encoding(true));

        await writer.WriteLineAsync(CsvField.Text(title));
        await writer.WriteLineAsync($"Pharmacy,{CsvField.Text(pharmacy)}");
        await writer.WriteLineAsync(periodLine);
        await writer.WriteLineAsync($"Exported,{CsvField.Timestamp(_clock.UtcNow)} UTC");
        await writer.WriteLineAsync();

        return writer;
    }

    /// <summary>
    /// Names the top-selling response shape for Swagger. <c>GridResult&lt;T&gt;</c> is generic and
    /// the attribute cannot express it inline without this.
    /// </summary>
    public sealed class GridResultOfTopSelling
        : PMS.SharedKernel.Grid.GridResult<TopSellingProductDto>;
}
