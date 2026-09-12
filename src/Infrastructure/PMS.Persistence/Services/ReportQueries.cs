using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Stock;
using PMS.Application.Common.Units;
using PMS.Application.Interfaces;
using PMS.Domain.Billing;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.Persistence.Contexts;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Interfaces;

namespace PMS.Persistence.Services;

/// <summary>
/// The reports.
///
/// <para><b>Every profit figure in this file is built from the same two expressions:</b> revenue
/// is <c>SaleLine.NetLineTotal</c> — the post-discount figure Module 5 split across the lines —
/// and cost is <c>QuantityInBaseUnits * Batch.PurchasePricePerBaseUnit</c>, the price of the
/// line's <em>own</em> batch. They are spelled out inline at each use rather than factored into a
/// helper, because EF cannot translate a method call into a <c>SUM</c>; <c>ProfitMath</c> holds
/// the same arithmetic for everything outside a query, and <c>ProfitMathTests</c> plus
/// <c>acceptance_reports.py</c> are what keep the two in step.</para>
///
/// <para><b>Cancelled sales are excluded by a filter, not zeroed.</b> <see cref="SoldLines"/> and
/// <see cref="CompletedSales"/> both apply it, and nothing here reads a sale any other way. A
/// zeroed sale would still be counted in every denominator — transactions per hour, average
/// basket, margin per sale — and each of those would be quietly wrong.</para>
///
/// <para><b>Nothing rounds.</b> SQL aggregates over unrounded decimals and the pages round once
/// when they print. Rounding per line and summing drifts by a paisa a line.</para>
///
/// <para>There is no WHERE on TenantId anywhere except on <c>Users</c>, and that is correct: every
/// other entity here is an <c>ITenantEntity</c> and the global query filter supplies the pharmacy.
/// <c>Users</c> is the global identity table, so reaching into it is an explicit join.</para>
/// </summary>
public sealed class ReportQueries : IReportQueries
{
    private readonly ApplicationDbContext _context;
    private readonly IOperatingExpenses _expenses;
    private readonly IDateTime _clock;

    public ReportQueries(
        ApplicationDbContext context, IOperatingExpenses expenses, IDateTime clock)
    {
        _context = context;
        _expenses = expenses;
        _clock = clock;
    }

    /// <summary>Today in UTC, spelled the same way as in every other query service.</summary>
    private DateOnly Today => _clock.UtcDateToday();

    private static DateTime StartOf(DateOnly date) =>
        date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    /// <summary>Exclusive upper bound covering the whole of <paramref name="date"/>.</summary>
    private static DateTime EndOf(DateOnly date) =>
        date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    /// <summary>
    /// Completed sales in a range. <b>Completed</b>: a cancelled sale restored its stock and
    /// reversed its payment, so it belongs in no total and no count.
    /// </summary>
    private IQueryable<Sale> CompletedSales(DateOnly from, DateOnly to)
    {
        var start = StartOf(from);
        var end = EndOf(to);

        return _context.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed)
            .Where(s => s.SaleDate >= start && s.SaleDate < end);
    }

    /// <summary>Sale lines belonging to those sales.</summary>
    private IQueryable<SaleLine> SoldLines(DateOnly from, DateOnly to)
    {
        var start = StartOf(from);
        var end = EndOf(to);

        return _context.SaleLines
            .AsNoTracking()
            .Where(l => l.Sale.Status == SaleStatus.Completed)
            .Where(l => l.Sale.SaleDate >= start && l.Sale.SaleDate < end);
    }

    /// <summary>
    /// Returns that <em>happened</em> in a range, whenever the sale they reverse was made.
    ///
    /// <para>Keyed on <c>CreatedOnUtc</c>, the moment the goods came back and the cash went out.
    /// A sale in August returned in September reduces September — which is when the money
    /// actually left, and what an owner reconciling a month against their till expects.</para>
    /// </summary>
    private IQueryable<SalesReturn> ReturnsIn(DateOnly from, DateOnly to)
    {
        var start = StartOf(from);
        var end = EndOf(to);

        return _context.SalesReturns
            .AsNoTracking()
            .Where(r => r.SaleLine.Sale.Status == SaleStatus.Completed)
            .Where(r => r.CreatedOnUtc >= start && r.CreatedOnUtc < end);
    }

    // ── 1. Daily ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<DailySalesReportDto> GetDailySalesReportAsync(
        DateOnly date, CancellationToken cancellationToken = default)
    {
        var sales = CompletedSales(date, date);
        var lines = SoldLines(date, date);
        var returns = ReturnsIn(date, date);

        var headline = await sales
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalSales = g.Sum(s => s.NetTotal),
                Transactions = g.Count(),
                Discount = g.Sum(s => s.DiscountAmount),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var lineTotals = await lines
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Revenue = g.Sum(l => l.NetLineTotal),
                Cost = g.Sum(l => l.QuantityInBaseUnits * l.Batch.PurchasePricePerBaseUnit),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var returnTotals = await returns
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Refunded = g.Sum(r => r.RefundAmount),
                Cost = g.Sum(r => r.QuantityReturnedInBaseUnits
                    * r.SaleLine.Batch.PurchasePricePerBaseUnit),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var returnImpact = (returnTotals?.Refunded ?? 0m) - (returnTotals?.Cost ?? 0m);

        var rows = await sales
            .OrderBy(s => s.SaleDate)
            .ThenBy(s => s.InvoiceNumber)
            .Select(s => new
            {
                s.Id,
                s.InvoiceNumber,
                s.SaleDate,
                s.Subtotal,
                s.DiscountAmount,
                s.NetTotal,

                // Per sale, over its own lines. A correlated aggregate rather than a second
                // query per row: the reader evaluates it once per sale in the same statement.
                Revenue = s.Lines.Sum(l => l.NetLineTotal),
                Cost = s.Lines.Sum(l =>
                    l.QuantityInBaseUnits * l.Batch.PurchasePricePerBaseUnit),

                CashierName = _context.Users
                    .Where(u => u.Id == s.CashierUserId)
                    .Select(u => u.FullName)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var hourly = await sales
            .GroupBy(s => s.SaleDate.Hour)
            .Select(g => new
            {
                Hour = g.Key,
                Count = g.Count(),
                Sales = g.Sum(s => s.NetTotal),
            })
            .ToListAsync(cancellationToken);

        var byHour = hourly.ToDictionary(h => h.Hour);

        return new DailySalesReportDto(
            date,
            headline?.TotalSales ?? 0m,
            headline?.Transactions ?? 0,
            headline?.Discount ?? 0m,

            // Gross profit: what the lines earned, less what the day's returns took back out.
            (lineTotals?.Revenue ?? 0m) - (lineTotals?.Cost ?? 0m) - returnImpact,
            returnTotals?.Refunded ?? 0m,
            returnTotals?.Count ?? 0,
            returnImpact,
            rows.Select(r => new DailySaleRowDto(
                r.Id, r.InvoiceNumber, r.SaleDate, r.CashierName,
                r.Subtotal, r.DiscountAmount, r.NetTotal, r.Cost, r.Revenue - r.Cost))
                .ToList(),

            // Every hour, including the empty ones: a bar chart with gaps where a shop was
            // simply quiet reads as missing data rather than as a quiet hour.
            Enumerable.Range(0, 24)
                .Select(hour => byHour.TryGetValue(hour, out var row)
                    ? new HourlySalesDto(hour, row.Count, row.Sales)
                    : new HourlySalesDto(hour, 0, 0m))
                .ToList());
    }

    // ── 2. Monthly ───────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<MonthlySalesReportDto> GetMonthlySalesReportAsync(
        int month, int year, CancellationToken cancellationToken = default)
    {
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);

        var sales = CompletedSales(first, last);
        var lines = SoldLines(first, last);
        var returns = ReturnsIn(first, last);

        var headline = await sales
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalSales = g.Sum(s => s.NetTotal),
                Transactions = g.Count(),
                Discount = g.Sum(s => s.DiscountAmount),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var lineTotals = await lines
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Revenue = g.Sum(l => l.NetLineTotal),
                Cost = g.Sum(l => l.QuantityInBaseUnits * l.Batch.PurchasePricePerBaseUnit),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var returnTotals = await returns
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Refunded = g.Sum(r => r.RefundAmount),
                Cost = g.Sum(r => r.QuantityReturnedInBaseUnits
                    * r.SaleLine.Batch.PurchasePricePerBaseUnit),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var returnImpact = (returnTotals?.Refunded ?? 0m) - (returnTotals?.Cost ?? 0m);
        var grossProfit = (lineTotals?.Revenue ?? 0m) - (lineTotals?.Cost ?? 0m) - returnImpact;

        // Per day: sales and transactions from the sales, profit from their lines, and the day's
        // own returns. Three passes rather than one because SQL Server will not aggregate over an
        // aggregate — the same rule Modules 6 and 7 both met.
        var dailySales = await sales
            .GroupBy(s => s.SaleDate.Date)
            .Select(g => new
            {
                Day = g.Key,
                Count = g.Count(),
                Sales = g.Sum(s => s.NetTotal),
            })
            .ToListAsync(cancellationToken);

        var dailyLines = await lines
            .GroupBy(l => l.Sale.SaleDate.Date)
            .Select(g => new
            {
                Day = g.Key,
                Revenue = g.Sum(l => l.NetLineTotal),
                Cost = g.Sum(l => l.QuantityInBaseUnits * l.Batch.PurchasePricePerBaseUnit),
            })
            .ToListAsync(cancellationToken);

        var dailyReturns = await returns
            .GroupBy(r => r.CreatedOnUtc.Date)
            .Select(g => new
            {
                Day = g.Key,
                Refunded = g.Sum(r => r.RefundAmount),
                Cost = g.Sum(r => r.QuantityReturnedInBaseUnits
                    * r.SaleLine.Batch.PurchasePricePerBaseUnit),
            })
            .ToListAsync(cancellationToken);

        var salesByDay = dailySales.ToDictionary(d => DateOnly.FromDateTime(d.Day));
        var linesByDay = dailyLines.ToDictionary(d => DateOnly.FromDateTime(d.Day));
        var returnsByDay = dailyReturns.ToDictionary(d => DateOnly.FromDateTime(d.Day));

        var days = new List<DailyBreakdownRowDto>();

        // Every day of the month, including days with nothing. A trend chart that skipped quiet
        // days would compress the x-axis and misrepresent the shape of the month.
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            salesByDay.TryGetValue(day, out var s);
            linesByDay.TryGetValue(day, out var l);
            returnsByDay.TryGetValue(day, out var r);

            var dayImpact = (r?.Refunded ?? 0m) - (r?.Cost ?? 0m);

            days.Add(new DailyBreakdownRowDto(
                day,
                s?.Count ?? 0,
                s?.Sales ?? 0m,
                (l?.Revenue ?? 0m) - (l?.Cost ?? 0m) - dayImpact));
        }

        // Called, not assumed. Returns zero until Module 9 fills it in — and because the report
        // goes through the interface today, completing Module 9 changes no code here.
        var operatingExpenses = await _expenses.GetOperatingExpensesAsync(
            first, last, cancellationToken);

        return new MonthlySalesReportDto(
            month,
            year,
            headline?.TotalSales ?? 0m,
            headline?.Transactions ?? 0,
            headline?.Discount ?? 0m,
            grossProfit,
            operatingExpenses,
            ProfitMath.NetProfit(grossProfit, operatingExpenses),
            returnTotals?.Refunded ?? 0m,
            returnImpact,
            days);
    }

    // ── 3. Top selling ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Per product: quantity, revenue and profit, net of returns.
    ///
    /// <para>Returns are subtracted from all three. A product sold forty times and returned ten
    /// shows thirty — a "top seller" list that counted goods the customer brought back would rank
    /// a product nobody kept above one they did.</para>
    /// </summary>
    private IQueryable<TopSellingRow> TopSellingRows(
        DateOnly from, DateOnly to, ProductType? productType)
    {
        var lines = SoldLines(from, to);
        var returns = ReturnsIn(from, to);

        if (productType is { } type)
        {
            lines = type == ProductType.Medicine
                ? lines.Where(l => l.Product.ProductType == ProductType.Medicine)
                : lines.Where(l => l.Product.ProductType != ProductType.Medicine);
        }

        var sold = lines
            .GroupBy(l => l.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(l => l.QuantityInBaseUnits),
                Revenue = g.Sum(l => l.NetLineTotal),
                Cost = g.Sum(l => l.QuantityInBaseUnits * l.Batch.PurchasePricePerBaseUnit),
            });

        var returned = returns
            .GroupBy(r => r.SaleLine.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(r => r.QuantityReturnedInBaseUnits),
                Refunded = g.Sum(r => r.RefundAmount),
                Cost = g.Sum(r => r.QuantityReturnedInBaseUnits
                    * r.SaleLine.Batch.PurchasePricePerBaseUnit),
            });

        return sold.Select(s => new TopSellingRow
        {
            Product = _context.Products.First(p => p.Id == s.ProductId),
            Quantity = s.Quantity
                - returned.Where(r => r.ProductId == s.ProductId)
                    .Select(r => r.Quantity).FirstOrDefault(),
            Revenue = s.Revenue
                - returned.Where(r => r.ProductId == s.ProductId)
                    .Select(r => r.Refunded).FirstOrDefault(),
            Cost = s.Cost
                - returned.Where(r => r.ProductId == s.ProductId)
                    .Select(r => r.Cost).FirstOrDefault(),
        });
    }

    private static IQueryable<TopSellingRow> Ordered(
        IQueryable<TopSellingRow> rows, TopSellingSort sortBy) => sortBy switch
    {
        TopSellingSort.Revenue => rows
            .OrderByDescending(r => r.Revenue).ThenBy(r => r.Product.BrandName),
        TopSellingSort.Profit => rows
            .OrderByDescending(r => r.Revenue - r.Cost).ThenBy(r => r.Product.BrandName),
        _ => rows
            .OrderByDescending(r => r.Quantity).ThenBy(r => r.Product.BrandName),
    };

    /// <inheritdoc />
    public async Task<GridResult<TopSellingProductDto>> GetTopSellingProductsAsync(
        DateOnly from,
        DateOnly to,
        TopSellingSort sortBy,
        ProductType? productType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var rows = TopSellingRows(from, to, productType);

        var total = await rows.CountAsync(cancellationToken);

        var items = await Ordered(rows, sortBy)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return GridResult<TopSellingProductDto>.Create(
            items.Select(ToTopSellingDto).ToList(), total, page, pageSize);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TopSellingProductDto> StreamTopSellingProductsAsync(
        DateOnly from,
        DateOnly to,
        TopSellingSort sortBy,
        ProductType? productType,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var rows = Ordered(TopSellingRows(from, to, productType), sortBy).AsAsyncEnumerable();

        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            yield return ToTopSellingDto(row);
        }
    }

    private static TopSellingProductDto ToTopSellingDto(TopSellingRow row)
    {
        var profit = row.Revenue - row.Cost;

        return new TopSellingProductDto(
            row.Product.Id,
            row.Product.BrandName,
            row.Product.GenericName,
            row.Product.ProductType,
            row.Quantity,
            UnitConversion.FromBaseUnits(row.Quantity, row.Product),
            row.Revenue,
            profit,
            ProfitMath.MarginPercent(profit, row.Revenue));
    }

    // ── 4. By product type ───────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<SalesByProductTypeRowDto>> GetSalesByProductTypeAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var sold = await SoldLines(from, to)
            .GroupBy(l => l.Product.ProductType)
            .Select(g => new
            {
                ProductType = g.Key,
                Lines = g.Count(),
                Quantity = g.Sum(l => l.QuantityInBaseUnits),
                Revenue = g.Sum(l => l.NetLineTotal),
                Cost = g.Sum(l => l.QuantityInBaseUnits * l.Batch.PurchasePricePerBaseUnit),
            })
            .ToListAsync(cancellationToken);

        var returned = await ReturnsIn(from, to)
            .GroupBy(r => r.SaleLine.Product.ProductType)
            .Select(g => new
            {
                ProductType = g.Key,
                Quantity = g.Sum(r => r.QuantityReturnedInBaseUnits),
                Refunded = g.Sum(r => r.RefundAmount),
                Cost = g.Sum(r => r.QuantityReturnedInBaseUnits
                    * r.SaleLine.Batch.PurchasePricePerBaseUnit),
            })
            .ToListAsync(cancellationToken);

        var returnsByType = returned.ToDictionary(r => r.ProductType);

        return sold
            .Select(row =>
            {
                returnsByType.TryGetValue(row.ProductType, out var back);

                var revenue = row.Revenue - (back?.Refunded ?? 0m);
                var cost = row.Cost - (back?.Cost ?? 0m);
                var profit = revenue - cost;

                return new SalesByProductTypeRowDto(
                    row.ProductType,
                    row.Lines,
                    row.Quantity - (back?.Quantity ?? 0),
                    revenue,
                    cost,
                    profit,
                    ProfitMath.MarginPercent(profit, revenue));
            })
            .OrderByDescending(row => row.Revenue)
            .ToList();
    }

    // ── 5. Dead stock ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Products holding stock that has not sold recently, or ever.
    ///
    /// <para><b>The never-sold case is the whole point, and it is why the last-sale lookup is a
    /// subquery rather than a join.</b> A product with no sale lines has nothing to join to; an
    /// inner join drops it, and the report then silently omits exactly the stock that has been
    /// sitting there longest. <c>FirstOrDefault</c> over a nullable projection gives null for
    /// those rows, which is the outer join written in a form EF reliably translates — the
    /// <c>GroupJoin</c> + <c>DefaultIfEmpty</c> spelling failed to translate in Module 6.</para>
    /// </summary>
    /// <summary>Active products, narrowed to one product type if one was asked for.</summary>
    private IQueryable<Product> FilteredProducts(ProductType? productType)
    {
        var products = _context.Products.AsNoTracking().Where(p => p.IsActive);

        if (productType is { } type)
        {
            products = type == ProductType.Medicine
                ? products.Where(p => p.ProductType == ProductType.Medicine)
                : products.Where(p => p.ProductType != ProductType.Medicine);
        }

        return products;
    }

    /// <summary>
    /// Batches that are physically on a shelf. Expired ones included — expired stock that has
    /// never sold is still capital sitting there, and it is more dead than anything else on the
    /// dead-stock report rather than less.
    /// </summary>
    private IQueryable<Batch> StockedBatches() => _context.Batches
        .AsNoTracking()
        .Where(b => b.IsActive && b.QuantityInBaseUnits > 0);

    /// <summary>
    /// <b>Which products count as dead stock</b>, as ids and nothing else.
    ///
    /// <para>Every total on this report is built from this query, and it is deliberately free of
    /// aggregates: it uses <c>Any</c>, not <c>Sum</c> or <c>Max</c>. Counting or summing over a
    /// projection whose columns are themselves aggregate subqueries is what SQL Server refuses
    /// with <em>"Cannot perform an aggregate function on an expression containing an aggregate or
    /// a subquery"</em> — which is exactly how this report first failed.</para>
    ///
    /// <para>The threshold test is expressed as <b>"has not sold since the cutoff"</b> rather than
    /// "the last sale was before the cutoff". The two describe the same set, but only the first
    /// covers the never-sold products without a <c>Max</c> over an outer join — and never-sold is
    /// the case this whole report exists for.</para>
    /// </summary>
    private IQueryable<Guid> DeadStockProductIds(int thresholdDays, ProductType? productType)
    {
        var cutoff = _clock.UtcToday.AddDays(-thresholdDays);

        var stocked = StockedBatches();

        var soldSinceCutoff = _context.SaleLines
            .AsNoTracking()
            .Where(l => l.Sale.Status == SaleStatus.Completed)
            .Where(l => l.Sale.SaleDate >= cutoff);

        return FilteredProducts(productType)
            // Any batch at all means the summed quantity is above zero, because every batch in
            // the set already has a positive quantity.
            .Where(p => stocked.Any(b => b.ProductId == p.Id))
            .Where(p => !soldSinceCutoff.Any(l => l.ProductId == p.Id))
            .Select(p => p.Id);
    }

    /// <summary>
    /// The rows themselves.
    ///
    /// <para><b>The never-sold case is why the last-sale lookup is a subquery rather than a
    /// join.</b> A product with no sale lines has nothing to join to; an inner join drops it, and
    /// the report then silently omits exactly the stock that has sat longest. <c>FirstOrDefault</c>
    /// over a nullable projection yields null for those rows — the outer join, written in a form
    /// EF reliably translates. (<c>GroupJoin</c> + <c>DefaultIfEmpty</c> failed to translate at
    /// all in Module 6.)</para>
    ///
    /// <para>Nothing aggregates over this projection. The counts and totals come from
    /// <see cref="DeadStockProductIds"/> and from the batch table directly.</para>
    /// </summary>
    private IQueryable<DeadStockRow> DeadStockRows(int thresholdDays, ProductType? productType)
    {
        var ids = DeadStockProductIds(thresholdDays, productType);

        var stocked = StockedBatches()
            .GroupBy(b => b.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(b => b.QuantityInBaseUnits),
                Value = g.Sum(b => b.QuantityInBaseUnits * b.PurchasePricePerBaseUnit),
            });

        var lastSold = _context.SaleLines
            .AsNoTracking()
            .Where(l => l.Sale.Status == SaleStatus.Completed)
            .GroupBy(l => l.ProductId)
            .Select(g => new { ProductId = g.Key, LastSold = g.Max(l => l.Sale.SaleDate) });

        return _context.Products
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new DeadStockRow
            {
                Product = p,
                Quantity = stocked.Where(s => s.ProductId == p.Id)
                    .Select(s => s.Quantity).FirstOrDefault(),
                Value = stocked.Where(s => s.ProductId == p.Id)
                    .Select(s => s.Value).FirstOrDefault(),

                // Nullable, and null is the answer for a product that has never been sold.
                LastSold = lastSold.Where(s => s.ProductId == p.Id)
                    .Select(s => (DateTime?)s.LastSold).FirstOrDefault(),
            });
    }

    private static IQueryable<DeadStockRow> DeadStockOrdered(IQueryable<DeadStockRow> rows) =>
        rows
            // Never-sold first: nothing is deader than stock that has never moved at all.
            .OrderBy(x => x.LastSold == null ? 0 : 1)
            .ThenBy(x => x.LastSold)
            .ThenBy(x => x.Product.BrandName);

    /// <inheritdoc />
    public async Task<GridResult<DeadStockRowDto>> GetDeadStockAsync(
        int thresholdDays,
        ProductType? productType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Counted over the id query, not over the rows: see DeadStockProductIds.
        var total = await DeadStockProductIds(thresholdDays, productType)
            .CountAsync(cancellationToken);

        var items = await DeadStockOrdered(DeadStockRows(thresholdDays, productType))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return GridResult<DeadStockRowDto>.Create(
            items.Select(row => ToDeadStockDto(row, Today)).ToList(),
            total, page, pageSize);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<DeadStockRowDto> StreamDeadStockAsync(
        int thresholdDays,
        ProductType? productType,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var today = Today;
        var rows = DeadStockOrdered(DeadStockRows(thresholdDays, productType)).AsAsyncEnumerable();

        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            yield return ToDeadStockDto(row, today);
        }
    }

    /// <inheritdoc />
    public async Task<DeadStockSummaryDto> GetDeadStockSummaryAsync(
        int thresholdDays,
        ProductType? productType,
        CancellationToken cancellationToken = default)
    {
        var ids = DeadStockProductIds(thresholdDays, productType);

        // Three statements rather than one grouped projection. SQL Server will not aggregate over
        // an expression containing an aggregate, and every column of a dead-stock row is one.
        var products = await ids.CountAsync(cancellationToken);

        var everSold = _context.SaleLines
            .AsNoTracking()
            .Where(l => l.Sale.Status == SaleStatus.Completed);

        var neverSold = await _context.Products
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Where(p => !everSold.Any(l => l.ProductId == p.Id))
            .CountAsync(cancellationToken);

        // Summed straight off the batch table, where the quantity and the price are plain columns.
        var value = await StockedBatches()
            .Where(b => ids.Contains(b.ProductId))
            .SumAsync(b => (decimal?)(b.QuantityInBaseUnits * b.PurchasePricePerBaseUnit),
                cancellationToken);

        return new DeadStockSummaryDto(thresholdDays, products, neverSold, value ?? 0m);
    }

    private static DeadStockRowDto ToDeadStockDto(DeadStockRow row, DateOnly today)
    {
        var lastSold = row.LastSold is { } sold ? DateOnly.FromDateTime(sold) : (DateOnly?)null;

        return new DeadStockRowDto(
            row.Product.Id,
            row.Product.BrandName,
            row.Product.GenericName,
            row.Product.ProductType,
            row.Quantity,
            UnitConversion.FromBaseUnits(row.Quantity, row.Product),
            lastSold,
            lastSold is { } date ? today.DayNumber - date.DayNumber : null,
            row.Value);
    }

    // ── 6. Sales per user ────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<SalesPerUserRowDto>> GetSalesPerUserAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var rows = await CompletedSales(from, to)
            .GroupBy(s => s.CashierUserId)
            .Select(g => new
            {
                UserId = g.Key,
                Transactions = g.Count(),
                Sales = g.Sum(s => s.NetTotal),
                Discount = g.Sum(s => s.DiscountAmount),

                // The denominator for the average discount: what the bills came to before any
                // discount. Dividing by net sales instead would understate every cashier's
                // percentage, and by more the more they discounted.
                Subtotal = g.Sum(s => s.Subtotal),
            })
            .ToListAsync(cancellationToken);

        var ids = rows.Select(r => r.UserId).ToList();

        var names = await _context.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync(cancellationToken);

        // Memberships are tenant-filtered, so this is the role held at THIS pharmacy — the same
        // person may be an Employee here and an Admin elsewhere.
        var roles = await _context.UserTenantMemberships
            .AsNoTracking()
            .Where(m => ids.Contains(m.UserId))
            .Select(m => new { m.UserId, m.Role })
            .ToListAsync(cancellationToken);

        var nameById = names.ToDictionary(n => n.Id, n => n.FullName);
        var roleById = roles.ToDictionary(r => r.UserId, r => (UserRole?)r.Role);

        return rows
            .Select(r => new SalesPerUserRowDto(
                r.UserId,

                // A cashier whose account has since been deleted still has sales, and hiding
                // them would hide those sales from the total.
                nameById.TryGetValue(r.UserId, out var name) ? name : "(unknown user)",
                roleById.TryGetValue(r.UserId, out var role) ? role : null,
                r.Transactions,
                r.Sales,
                r.Discount,
                ProfitMath.MarginPercent(r.Discount, r.Subtotal)))
            .OrderByDescending(r => r.TotalSales)
            .ToList();
    }

    // ── 8. Stock valuation ───────────────────────────────────────────────────────────────

    /// <summary>
    /// What is on the shelves and what it cost.
    ///
    /// <para><b>Expired batches are counted separately and excluded from the value.</b> Expired
    /// stock cannot be sold — FEFO refuses it and the till refuses it — so counting it as an
    /// asset would overstate the single figure an owner reads as "money tied up in
    /// inventory".</para>
    /// </summary>
    private IQueryable<StockValuationRow> StockValuationRows(ProductType? productType)
    {
        var today = Today;
        var stocked = StockedBatches();

        // Fefo.Sellable, not a restatement of its three conditions. Composed out here rather than
        // inside the projection below, which is what keeps it translatable — Module 3's note. The
        // valuation and the till therefore agree by construction about what can be sold.
        var live = stocked
            .Sellable(today)
            .GroupBy(b => b.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(b => b.QuantityInBaseUnits),
                Value = g.Sum(b => b.QuantityInBaseUnits * b.PurchasePricePerBaseUnit),
            });

        var expired = stocked
            .Where(b => b.ExpiryDate != null && b.ExpiryDate < today)
            .GroupBy(b => b.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(b => b.QuantityInBaseUnits),
                Value = g.Sum(b => b.QuantityInBaseUnits * b.PurchasePricePerBaseUnit),
            });

        // Filtered by Any, not by a positive sum over the projection - the same reason the dead
        // stock report keeps its id query aggregate-free. A product with any stocked batch has a
        // positive total, because every batch in the set already does.
        var ids = ValuedProductIds(productType);

        return _context.Products
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new StockValuationRow
            {
                Product = p,
                Quantity = live.Where(s => s.ProductId == p.Id)
                    .Select(s => s.Quantity).FirstOrDefault(),
                Value = live.Where(s => s.ProductId == p.Id)
                    .Select(s => s.Value).FirstOrDefault(),
                ExpiredQuantity = expired.Where(s => s.ProductId == p.Id)
                    .Select(s => s.Quantity).FirstOrDefault(),
                ExpiredValue = expired.Where(s => s.ProductId == p.Id)
                    .Select(s => s.Value).FirstOrDefault(),
            });
    }

    /// <summary>Products with anything at all on the shelf, as ids and free of aggregates.</summary>
    private IQueryable<Guid> ValuedProductIds(ProductType? productType)
    {
        var stocked = StockedBatches();

        return FilteredProducts(productType)
            .Where(p => stocked.Any(b => b.ProductId == p.Id))
            .Select(p => p.Id);
    }

    /// <inheritdoc />
    public async Task<GridResult<StockValuationRowDto>> GetStockValuationAsync(
        ProductType? productType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var total = await ValuedProductIds(productType).CountAsync(cancellationToken);

        var items = await StockValuationRows(productType)
            // Most valuable first: this report is read to find out where the money is.
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Product.BrandName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return GridResult<StockValuationRowDto>.Create(
            items.Select(ToValuationDto).ToList(), total, page, pageSize);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StockValuationRowDto> StreamStockValuationAsync(
        ProductType? productType,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var rows = StockValuationRows(productType)
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Product.BrandName)
            .AsAsyncEnumerable();

        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            yield return ToValuationDto(row);
        }
    }

    /// <inheritdoc />
    public async Task<StockValuationSummaryDto> GetStockValuationSummaryAsync(
        ProductType? productType, CancellationToken cancellationToken = default)
    {
        var ids = ValuedProductIds(productType);
        var today = Today;

        // Aggregated off the batch table, where quantity and price are plain columns, rather than
        // over the row projection - whose every column is itself an aggregate subquery, which SQL
        // Server refuses to aggregate over.
        var batches = StockedBatches().Where(b => ids.Contains(b.ProductId));

        var products = await ids.CountAsync(cancellationToken);

        var live = await batches
            .Sellable(today)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Quantity = g.Sum(b => b.QuantityInBaseUnits),
                Value = g.Sum(b => b.QuantityInBaseUnits * b.PurchasePricePerBaseUnit),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var expired = await batches
            .Where(b => b.ExpiryDate != null && b.ExpiryDate < today)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Quantity = g.Sum(b => b.QuantityInBaseUnits),
                Value = g.Sum(b => b.QuantityInBaseUnits * b.PurchasePricePerBaseUnit),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new StockValuationSummaryDto(
            products,
            live?.Quantity ?? 0,
            live?.Value ?? 0m,
            expired?.Quantity ?? 0,
            expired?.Value ?? 0m);
    }

    private static StockValuationRowDto ToValuationDto(StockValuationRow row) =>
        new(
            row.Product.Id,
            row.Product.BrandName,
            row.Product.GenericName,
            row.Product.ProductType,
            row.Quantity,
            UnitConversion.FromBaseUnits(row.Quantity, row.Product),

            // Weighted by quantity, not an average of the batch prices. Ten units at 1 and one
            // at 100 average 1.09 a unit, not 50.50.
            ProfitMath.WeightedAverageCost(row.Value, row.Quantity),
            row.Value,
            row.ExpiredQuantity,
            row.ExpiredValue);

    // ── Projection shapes ────────────────────────────────────────────────────────────────

    private sealed class TopSellingRow
    {
        public Product Product { get; init; } = null!;

        public int Quantity { get; init; }

        public decimal Revenue { get; init; }

        public decimal Cost { get; init; }
    }

    private sealed class DeadStockRow
    {
        public Product Product { get; init; } = null!;

        public int Quantity { get; init; }

        public decimal Value { get; init; }

        /// <summary>Null for a product that has never been sold. See the query.</summary>
        public DateTime? LastSold { get; init; }
    }

    private sealed class StockValuationRow
    {
        public Product Product { get; init; } = null!;

        public int Quantity { get; init; }

        public decimal Value { get; init; }

        public int ExpiredQuantity { get; init; }

        public decimal ExpiredValue { get; init; }
    }
}
