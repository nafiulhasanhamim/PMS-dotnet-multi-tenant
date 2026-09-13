using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Stock;
using PMS.Application.Common.Units;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.Persistence.Contexts;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Interfaces;

namespace PMS.Persistence.Services;

/// <summary>
/// The alert reads. Four queries over tables that already existed.
///
/// <para><b>There is no WHERE on TenantId anywhere in this file, and that is correct.</b>
/// <c>Product</c> and <c>Batch</c> both implement <c>ITenantEntity</c>, so the global query
/// filter adds it to every query below — including inside the aggregates, which is what makes
/// two pharmacies holding the same catalogue product see only their own totals.</para>
///
/// <para><b>Two rules run through everything here.</b> A null expiry date is excluded from both
/// the expiring and the expired list rather than sorted to one end; and expired stock does not
/// count towards what a product has available. Both are stated once at each query and are the
/// two things most worth checking if a number ever looks wrong.</para>
/// </summary>
public sealed class AlertQueries : IAlertQueries
{
    private readonly ApplicationDbContext _context;
    private readonly IDateTime _clock;

    public AlertQueries(ApplicationDbContext context, IDateTime clock)
    {
        _context = context;
        _clock = clock;
    }

    private DateOnly Today => _clock.UtcDateToday();

    /// <summary>
    /// Batches that are on a shelf: active, and holding something.
    ///
    /// <para>Deliberately <em>not</em> <c>Fefo.Sellable</c>, which also excludes expired stock.
    /// Two of the three lists here are about expired stock, so the sellable rule would filter
    /// away the rows the module exists to show. Where the sellable rule is wanted — the
    /// low-stock total — it is composed explicitly, and by the shared helper rather than by a
    /// second copy of the predicate.</para>
    /// </summary>
    private IQueryable<Batch> StockedBatches =>
        _context.Batches.AsNoTracking().Where(b => b.IsActive && b.QuantityInBaseUnits > 0);

    /// <inheritdoc />
    public async Task<GridResult<ExpiringBatchDto>> GetExpiringBatchesAsync(
        int daysAhead,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var today = Today;
        var cutoff = today.AddDays(daysAhead);

        // ExpiryDate != null first, and it is not redundant with the range comparison. In SQL a
        // NULL comparison is UNKNOWN rather than false, so the range alone would already
        // exclude these rows — but silently, and only for as long as nobody rewrites the
        // predicate. Saying it out loud is the difference between a rule and an accident.
        var query = StockedBatches
            .Where(b => b.ExpiryDate != null
                && b.ExpiryDate >= today
                && b.ExpiryDate <= cutoff);

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderBy(b => b.ExpiryDate)
            .ThenBy(b => b.BatchNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new BatchRow { Batch = b, Product = b.Product })
            .ToListAsync(cancellationToken);

        return GridResult<ExpiringBatchDto>.Create(
            rows.Select(row => ToDto(row, today)).ToList(), total, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<GridResult<ExpiringBatchDto>> GetExpiredBatchesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var today = Today;

        var query = StockedBatches
            .Where(b => b.ExpiryDate != null && b.ExpiryDate < today);

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            // Ascending, which puts the most overdue first: the oldest date is the one that has
            // been sitting there longest.
            .OrderBy(b => b.ExpiryDate)
            .ThenBy(b => b.BatchNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new BatchRow { Batch = b, Product = b.Product })
            .ToListAsync(cancellationToken);

        return GridResult<ExpiringBatchDto>.Create(
            rows.Select(row => ToDto(row, today)).ToList(), total, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<GridResult<LowStockProductDto>> GetLowStockProductsAsync(
        ProductType? productType,
        LowStockStatusFilter status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var today = Today;

        var scored = ScoredProducts(today, productType);

        var filtered = status switch
        {
            LowStockStatusFilter.Low => scored.Where(x => x.Available > 0),
            LowStockStatusFilter.OutOfStock => scored.Where(x => x.Available <= 0),
            _ => scored,
        };

        var total = await filtered.CountAsync(cancellationToken);

        var rows = await filtered
            // Ratio, not absolute quantity: ten of a product that should hold twenty is a
            // different situation from ten of one that should hold a thousand, and the list is
            // read top-down by somebody deciding what to order first.
            //
            // The guard is for a reorder level of zero, which is legitimate — a product nobody
            // wants to be told about — and would otherwise be a divide by zero. Such a product
            // only reaches this list at all when it has nothing left, so sorting it first is
            // also right.
            .OrderBy(x => x.Product.ReorderLevel <= 0
                ? 0d
                : (double)x.Available / x.Product.ReorderLevel)
            .ThenBy(x => x.Product.BrandName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new LowStockProductDto(
            row.Product.Id,
            row.Product.BrandName,
            row.Product.GenericName,
            row.Product.ProductType,
            row.Available,
            UnitConversion.FromBaseUnits(row.Available, row.Product),
            row.Product.ReorderLevel,
            row.Product.BaseUnitName,
            row.Available <= 0 ? LowStockStatus.OutOfStock : LowStockStatus.Low,
            row.Expired,
            row.Expired > 0
                ? UnitConversion.FromBaseUnits(row.Expired, row.Product)
                : null))
            .ToList();

        return GridResult<LowStockProductDto>.Create(items, total, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<AlertSummaryDto> GetAlertSummaryAsync(
        int expiryWindowDays,
        CancellationToken cancellationToken = default)
    {
        var today = Today;
        var cutoff = today.AddDays(expiryWindowDays);

        // ── One pass for both batch counts ──────────────────────────────────────────────
        //
        // GroupBy(_ => 1) is how a single row of aggregates is expressed in LINQ; it becomes a
        // SELECT with two conditional COUNTs and no GROUP BY column. Two separate CountAsync
        // calls would be two round trips over the same rows for no benefit.
        var batchCounts = await StockedBatches
            .Where(b => b.ExpiryDate != null)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                ExpiringSoon = g.Count(b => b.ExpiryDate >= today && b.ExpiryDate <= cutoff),
                Expired = g.Count(b => b.ExpiryDate < today),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // ── One statement per product count, and not by choice ──────────────────────────
        //
        // These two cannot share a pass the way the batch counts do. Each product's available
        // total is a correlated subquery, and SQL Server refuses to aggregate over an expression
        // containing one: "Cannot perform an aggregate function on an expression containing an
        // aggregate or a subquery". A COUNT with that subquery in the WHERE instead of the
        // SELECT is fine, which is what these two produce.
        //
        // So the summary is four round trips rather than three. Worth knowing before reaching
        // for a conditional aggregate here again.
        var scored = ScoredProducts(today, productType: null);

        var lowCount = await scored
            .Where(x => x.Available > 0)
            .CountAsync(cancellationToken);

        var outOfStockCount = await scored
            .Where(x => x.Available <= 0)
            .CountAsync(cancellationToken);

        // ── The preview line ────────────────────────────────────────────────────────────
        //
        // Scoped to the same window as the count on the card above it. A preview naming
        // something outside the counted set — an already-expired batch, or one eleven months
        // out — would read as a contradiction of the number beside it.
        var nearest = await StockedBatches
            .Where(b => b.ExpiryDate != null
                && b.ExpiryDate >= today
                && b.ExpiryDate <= cutoff)
            .OrderBy(b => b.ExpiryDate)
            .ThenBy(b => b.BatchNumber)
            .Select(b => new
            {
                b.ProductId,
                b.Product.BrandName,
                b.BatchNumber,
                b.ExpiryDate,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new AlertSummaryDto(
            batchCounts?.ExpiringSoon ?? 0,
            batchCounts?.Expired ?? 0,
            lowCount,
            outOfStockCount,
            expiryWindowDays,
            nearest is null
                ? null
                : new NearestExpiryDto(
                    nearest.ProductId,
                    nearest.BrandName,
                    nearest.BatchNumber,
                    nearest.ExpiryDate!.Value,
                    DaysBetween(today, nearest.ExpiryDate.Value)));
    }

    /// <summary>
    /// Active products with their sellable and expired totals, already filtered to those at or
    /// below their reorder level.
    ///
    /// <para><b>Composed here and shared by the list and the counts, so the dashboard card and
    /// the page it links to can never disagree.</b> Two callers of one expression rather than
    /// two expressions that happen to match today.</para>
    ///
    /// <para><b>The missing-group case is the part to be careful about.</b> A product with no
    /// batch rows at all has nothing to aggregate, and an inner join would drop it — silently
    /// omitting exactly the products that have never been stocked or have had every batch
    /// deleted, which are the clearest out-of-stock rows there are. Referencing the totals as a
    /// subquery handles it without a join at all: <c>FirstOrDefault</c> over no rows is 0.</para>
    ///
    /// <para>The textbook form for this is <c>GroupJoin</c> + <c>DefaultIfEmpty</c>, and it was
    /// written that way first. EF8 could not translate it — the endpoint returned "The LINQ
    /// expression could not be translated" rather than failing at compile time, which is the
    /// argument for having called the endpoint before building four screens on top of it.</para>
    ///
    /// <para>The aggregates are built <em>outside</em> the lambdas on purpose.
    /// <c>Fefo.Sellable</c> is an extension that composes an expression; called inside a lambda
    /// it would sit in the tree as a method call EF cannot translate and the query would fail at
    /// runtime. Composed out here it is ordinary C# building an <c>IQueryable</c>, and
    /// referencing it below is translated as a derived table. Module 3's stock list has the same
    /// note for the same reason.</para>
    /// </summary>
    private IQueryable<ScoredProduct> ScoredProducts(DateOnly today, ProductType? productType)
    {
        // Deactivated products are excluded. A deactivated product may still hold stock, but it
        // is not something the pharmacy is selling — telling somebody to reorder it would be
        // telling them to undo a decision they made on purpose.
        var products = _context.Products.AsNoTracking().Where(p => p.IsActive);

        if (productType is { } type)
        {
            products = type == ProductType.Medicine
                ? products.Where(p => p.ProductType == ProductType.Medicine)
                : products.Where(p => p.ProductType != ProductType.Medicine);
        }

        // The shared sellable rule: active, in stock, and not past its expiry date — with a null
        // expiry counting as sellable, because stock that never expires never stops being
        // sellable. This is the same helper FEFO deducts through, which is what makes "available"
        // here mean the same thing as "available" at the till.
        var sellableTotals = _context.Batches
            .AsNoTracking()
            .Sellable(today)
            .GroupBy(b => b.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(b => b.QuantityInBaseUnits),
            });

        var expiredTotals = StockedBatches
            .Where(b => b.ExpiryDate != null && b.ExpiryDate < today)
            .GroupBy(b => b.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(b => b.QuantityInBaseUnits),
            });

        return products
            .Select(product => new ScoredProduct
            {
                Product = product,
                Available = sellableTotals
                    .Where(t => t.ProductId == product.Id)
                    .Select(t => t.Quantity)
                    .FirstOrDefault(),
                Expired = expiredTotals
                    .Where(t => t.ProductId == product.Id)
                    .Select(t => t.Quantity)
                    .FirstOrDefault(),
            })

            // At or below, so a product sitting exactly on its reorder level is reported. The
            // level is the point at which somebody wanted to be told, not the point after it.
            .Where(x => x.Available <= x.Product.ReorderLevel);
    }

    private static ExpiringBatchDto ToDto(BatchRow row, DateOnly today)
    {
        // Not null: both queries filter on ExpiryDate != null, which is the only way a row
        // reaches here. The bang is the assertion, and the filter is the proof.
        var expiry = row.Batch.ExpiryDate!.Value;
        var days = DaysBetween(today, expiry);

        return new ExpiringBatchDto(
            row.Batch.Id,
            row.Batch.ProductId,
            row.Product.BrandName,
            row.Product.GenericName,
            row.Product.ProductType,
            row.Batch.BatchNumber,
            expiry,
            days,
            row.Batch.QuantityInBaseUnits,

            // Module 2's formatter, never a hardcoded unit word: "1 box + 2 strips" for one
            // product and "30 bags" for another, from the product's own configuration.
            UnitConversion.FromBaseUnits(row.Batch.QuantityInBaseUnits, row.Product),
            row.Product.BaseUnitName,
            StockPolicy.SeverityFor(days),
            row.Batch.SupplierId,
            row.Batch.SupplierNameText);
    }

    /// <summary>
    /// Whole days from <paramref name="today"/> to <paramref name="date"/>, negative once past.
    ///
    /// <para>Computed in memory rather than in SQL. <c>DateOnly.DayNumber</c> has no SQL
    /// translation, and the alternative is a provider-specific DATEDIFF — for at most a page of
    /// rows, subtracting two integers after materialising is both simpler and portable.</para>
    /// </summary>
    private static int DaysBetween(DateOnly today, DateOnly date) =>
        date.DayNumber - today.DayNumber;

    private sealed class BatchRow
    {
        public Batch Batch { get; init; } = null!;

        public Product Product { get; init; } = null!;
    }

    /// <summary>
    /// One product with its two totals, as EF projects it. The entity is carried rather than its
    /// individual columns because the formatter needs the whole unit configuration, and a page
    /// is twenty-five rows.
    /// </summary>
    private sealed class ScoredProduct
    {
        public Product Product { get; init; } = null!;

        public int Available { get; init; }

        public int Expired { get; init; }
    }

}
