using PMS.Application.Common.DTOs;
using PMS.Application.Common.Stock;
using PMS.Application.Common.Units;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Persistence.Contexts;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Interfaces;
using Microsoft.EntityFrameworkCore;
using DomainProductType = PMS.Domain.Enums.ProductType;

namespace PMS.Persistence.Services;

/// <summary>
/// Read-side projections over the current pharmacy's stock.
///
/// <para><b>There is no WHERE on TenantId anywhere in this file, including in the
/// aggregates.</b> <c>Batch</c> and <c>StockAdjustment</c> both implement
/// <c>ITenantEntity</c>, so the global query filter is applied to the correlated subqueries
/// below exactly as it is to a plain read — which is what makes two pharmacies that imported
/// the same catalogue medicine see only their own totals for it.</para>
///
/// <para><b>Entities are projected, then formatted in memory.</b> Every quantity on screen is
/// phrased using the product's own unit names through Module 2's converter, and that converter
/// needs the product. So these queries select the <c>Product</c> entity alongside the SQL
/// aggregates and do the phrasing afterwards — one round trip, no N+1, and no second
/// implementation of the packing arithmetic.</para>
/// </summary>
public sealed class StockQueries : IStockQueries
{
    private readonly ApplicationDbContext _context;
    private readonly IDateTime _clock;

    public StockQueries(ApplicationDbContext context, IDateTime clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>
    /// Today, in UTC.
    ///
    /// <para>Read once per query rather than per row: a query that straddled midnight while
    /// evaluating would otherwise classify its first rows against one date and its last
    /// against another, and the rows that disagreed would be exactly the ones expiring
    /// today.</para>
    /// </summary>
    private DateOnly Today => _clock.UtcDateToday();

    public async Task<GridResult<StockListItemDto>> ListAsync(
        string? search,
        StockStatusFilter stockStatus,
        ExpiryStatusFilter expiryStatus,
        StockProductTypeFilter productType,
        int expiringSoonWindowDays,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var today = Today;
        var soonCutoff = today.AddDays(expiringSoonWindowDays);

        // Inactive products are excluded. A deactivated product may still hold stock, and
        // that stock is still findable through the product itself — but it is not something
        // the pharmacy is selling, so it does not belong in the list used to decide what to
        // reorder.
        var products = _context.Products.AsNoTracking().Where(p => p.IsActive);

        products = productType switch
        {
            StockProductTypeFilter.Medicine =>
                products.Where(p => p.ProductType == DomainProductType.Medicine),
            StockProductTypeFilter.Other =>
                products.Where(p => p.ProductType != DomainProductType.Medicine),
            _ => products,
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            // Contains rather than a prefix, matching the product list: somebody searching
            // "paracetamol" expects to reach Napa through its generic name. A pharmacy holds
            // hundreds of products, so the scan is cheap — the opposite trade-off from the
            // 21,714-row shared catalogue, deliberately.
            products = products.Where(p =>
                EF.Functions.Like(p.BrandName, $"%{term}%")
                || (p.GenericName != null && EF.Functions.Like(p.GenericName, $"%{term}%")));
        }

        // ── The aggregates, composed as their own queryables ────────────────────────────
        //
        // Built here, outside any lambda, and that placement is load-bearing rather than
        // stylistic. Fefo.Sellable is an extension that composes an expression; called
        // *inside* a lambda it would sit in the expression tree as a method call EF cannot
        // translate, and the query would fail at runtime. Composed out here it is ordinary
        // C# building an IQueryable, and referencing that queryable from a lambda below is
        // translated as a subquery — which is how the sellable rule gets used by both the
        // filters and the projection without being written twice.
        var sellableTotals = _context.Batches
            .Sellable(today)
            .GroupBy(b => b.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(b => b.QuantityInBaseUnits),
            });

        // Batches holding stock, expired ones included: they are on the shelf and somebody
        // has to deal with them, so a count that omitted them would disagree with the detail
        // page for no reason a user could work out.
        var stockedBatches = _context.Batches
            .Where(b => b.IsActive && b.QuantityInBaseUnits > 0);

        var batchCounts = stockedBatches
            .GroupBy(b => b.ProductId)
            .Select(g => new { ProductId = g.Key, Count = g.Count() });

        var expiredTotals = stockedBatches
            .Where(b => b.ExpiryDate != null && b.ExpiryDate < today)
            .GroupBy(b => b.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(b => b.QuantityInBaseUnits),
            });

        // Nulls are excluded from "nearest" rather than treated as far-future: a product whose
        // stock never expires has no nearest expiry, and MIN over an empty set is NULL, which
        // is exactly the em dash the column wants.
        var nearestExpiries = stockedBatches
            .Where(b => b.ExpiryDate != null)
            .GroupBy(b => b.ProductId)
            .Select(g => new { ProductId = g.Key, Expiry = g.Min(b => b.ExpiryDate) });

        products = stockStatus switch
        {
            StockStatusFilter.InStock => products.Where(p =>
                sellableTotals.Any(t => t.ProductId == p.Id && t.Quantity > 0)),

            // A product with no batches at all has no row in sellableTotals, so "out of stock"
            // has to mean "no row, or a row summing to zero" rather than a comparison — the
            // absent row is the commonest case of all and a join would drop it.
            StockStatusFilter.OutOfStock => products.Where(p =>
                !sellableTotals.Any(t => t.ProductId == p.Id && t.Quantity > 0)),

            StockStatusFilter.LowStock => products.Where(p =>
                sellableTotals.Any(t =>
                    t.ProductId == p.Id && t.Quantity > 0 && t.Quantity <= p.ReorderLevel)),

            _ => products,
        };

        products = expiryStatus switch
        {
            // "Expiring soon" excludes what has already expired, and the expired filter is
            // separate: a product holding both is matched by both, which is right, because it
            // has both problems.
            ExpiryStatusFilter.ExpiringSoon => products.Where(p =>
                stockedBatches.Any(b =>
                    b.ProductId == p.Id
                    && b.ExpiryDate != null
                    && b.ExpiryDate >= today
                    && b.ExpiryDate <= soonCutoff)),

            ExpiryStatusFilter.Expired => products.Where(p =>
                expiredTotals.Any(t => t.ProductId == p.Id)),

            _ => products,
        };

        var total = await products.CountAsync(cancellationToken);

        // Ordered and paged on the entity query, then projected. Projecting first and ordering
        // afterwards is the mistake that cost an afternoon in Module 2: EF cannot order by a
        // member of an anonymous type it has already projected.
        var rows = await products
            .OrderBy(p => p.BrandName)
            .ThenBy(p => p.Strength)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new StockRow
            {
                Product = p,

                // The headline figure is *sellable* stock. See the DTO for why expired stock
                // is reported separately rather than added in.
                //
                // FirstOrDefault over the grouped queryable rather than a join, so a product
                // with no batches yields 0 instead of vanishing from the page.
                SellableQuantity = sellableTotals
                    .Where(t => t.ProductId == p.Id)
                    .Select(t => (int?)t.Quantity)
                    .FirstOrDefault() ?? 0,

                ExpiredQuantity = expiredTotals
                    .Where(t => t.ProductId == p.Id)
                    .Select(t => (int?)t.Quantity)
                    .FirstOrDefault() ?? 0,

                BatchCount = batchCounts
                    .Where(c => c.ProductId == p.Id)
                    .Select(c => (int?)c.Count)
                    .FirstOrDefault() ?? 0,

                NearestExpiry = nearestExpiries
                    .Where(n => n.ProductId == p.Id)
                    .Select(n => n.Expiry)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new StockListItemDto(
            row.Product.Id,
            row.Product.BrandName,
            row.Product.GenericName,
            row.Product.ProductType,
            row.Product.IsAntibiotic,
            row.Product.BaseUnitName,
            row.SellableQuantity,
            UnitConversion.FromBaseUnits(row.SellableQuantity, row.Product),
            row.BatchCount,
            row.NearestExpiry,
            row.NearestExpiry is { } expiry ? expiry.DayNumber - today.DayNumber : null,
            row.Product.ReorderLevel,
            StockMapping.StatusOf(row.SellableQuantity, row.Product.ReorderLevel),
            row.ExpiredQuantity,
            row.ExpiredQuantity > 0
                ? UnitConversion.FromBaseUnits(row.ExpiredQuantity, row.Product)
                : null,

            // Derived from the nearest expiry, which already includes expired batches - so a
            // product holding anything past its date reports a negative "days until" and
            // comes back Expired without a special case. Computed here rather than in a view
            // so that the amber window is defined in exactly one place.
            StockMapping.StateOf(
                row.NearestExpiry is { } near ? near.DayNumber - today.DayNumber : null,
                expiringSoonWindowDays)))
            .ToList();

        return GridResult<StockListItemDto>.Create(items, total, page, pageSize);
    }

    public async Task<ProductStockDto?> GetProductStockAsync(
        Guid productId,
        bool includePurchasePrices,
        int expiringSoonWindowDays,
        int depletedPage,
        int depletedPageSize,
        CancellationToken cancellationToken = default)
    {
        var today = Today;

        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product is null)
        {
            // Another pharmacy's product id looks exactly like this from here, which is the
            // whole point of the filter.
            return null;
        }

        var batches = _context.Batches.AsNoTracking().Where(b => b.ProductId == productId && b.IsActive);

        // Everything with stock left, ordered FEFO — the *ordering* only, not the sellable
        // filter. An expired batch that still holds stock belongs at the top of this list, in
        // red: it is the most urgent thing on the page, and hiding it would leave somebody
        // wondering where the missing quantity went.
        var active = await batches
            .Where(b => b.QuantityInBaseUnits > 0)
            .InFefoOrder()
            .ToListAsync(cancellationToken);

        var depletedQuery = batches.Where(b => b.QuantityInBaseUnits <= 0);
        var depletedCount = await depletedQuery.CountAsync(cancellationToken);

        // Newest first, unlike the active list. A depleted batch is history, and the recent
        // history is what anybody scrolling this section is looking for.
        var depleted = await depletedQuery
            .OrderByDescending(b => b.CreatedOnUtc)
            .Skip((depletedPage - 1) * depletedPageSize)
            .Take(depletedPageSize)
            .ToListAsync(cancellationToken);

        var sellable = active
            .Where(b => b.ExpiryDate is null || b.ExpiryDate >= today)
            .Sum(b => b.QuantityInBaseUnits);

        var expired = active
            .Where(b => b.ExpiryDate is not null && b.ExpiryDate < today)
            .Sum(b => b.QuantityInBaseUnits);

        var nearestExpiry = active
            .Where(b => b.ExpiryDate is not null)
            .Select(b => b.ExpiryDate)
            .DefaultIfEmpty(null)
            .Min();

        return new ProductStockDto(
            product.Id,
            product.BrandName,
            product.GenericName,
            product.Strength,
            product.ProductType,
            product.IsAntibiotic,
            product.BaseUnitName,
            UnitConversion.DescribePacking(product),
            product.PricePerBase,
            product.ReorderLevel,
            sellable,
            UnitConversion.FromBaseUnits(sellable, product),
            active.Count,
            nearestExpiry,
            nearestExpiry is { } expiry ? expiry.DayNumber - today.DayNumber : null,
            StockMapping.StatusOf(sellable, product.ReorderLevel),
            active
                .Select(b => StockMapping.ToDto(
                    b, product, includePurchasePrices, today, expiringSoonWindowDays))
                .ToList(),
            depletedCount,
            depleted
                .Select(b => StockMapping.ToDto(
                    b, product, includePurchasePrices, today, expiringSoonWindowDays))
                .ToList(),
            expired,
            expired > 0 ? UnitConversion.FromBaseUnits(expired, product) : null,
            StockMapping.StateOf(
                nearestExpiry is { } nearest ? nearest.DayNumber - today.DayNumber : null,
                expiringSoonWindowDays));
    }

    public async Task<BatchDto?> FindBatchAsync(
        Guid batchId,
        bool includePurchasePrices,
        int expiringSoonWindowDays,
        CancellationToken cancellationToken = default)
    {
        // Include the product: the DTO phrases every quantity in its unit names.
        var batch = await _context.Batches
            .AsNoTracking()
            .Include(b => b.Product)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        return batch is null
            ? null
            : StockMapping.ToDto(
                batch, batch.Product, includePurchasePrices, Today, expiringSoonWindowDays);
    }

    public async Task<GridResult<StockAdjustmentDto>> ListAdjustmentsAsync(
        Guid batchId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var adjustments = _context.StockAdjustments
            .AsNoTracking()
            .Where(a => a.BatchId == batchId);

        var total = await adjustments.CountAsync(cancellationToken);

        var rows = await adjustments
            .OrderByDescending(a => a.CreatedOnUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AdjustmentRow
            {
                Adjustment = a,
                Batch = a.Batch,
                Product = a.Batch.Product,

                // An explicit left join to the global Users table rather than a mapped
                // navigation. Users has no TenantId — one person can work at two pharmacies —
                // so this is a tenant-scoped row reaching into unfiltered data, and writing
                // it out keeps that crossing visible instead of burying it in the model.
                AdjustedByName = _context.Users
                    .Where(u => u.Id == a.AdjustedByUserId)
                    .Select(u => u.FullName)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new StockAdjustmentDto(
            row.Adjustment.Id,
            row.Adjustment.BatchId,
            row.Batch.BatchNumber,
            row.Adjustment.AdjustmentType,
            row.Adjustment.QuantityChangeInBaseUnits,
            StockMapping.FormatChange(row.Adjustment.QuantityChangeInBaseUnits, row.Product),
            row.Adjustment.QuantityBeforeInBaseUnits,
            row.Adjustment.QuantityAfterInBaseUnits,
            row.Adjustment.Reason,
            row.Adjustment.AdjustedByUserId,
            row.AdjustedByName,
            row.Adjustment.CreatedOnUtc))
            .ToList();

        return GridResult<StockAdjustmentDto>.Create(items, total, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Batch>> GetActiveBatchesFefoAsync(
        Guid productId, CancellationToken cancellationToken = default)
        // Tracked, not AsNoTracking. Its consumer is Module 5, which will take quantity from
        // each batch in turn and save the result; no-tracking entities would silently discard
        // those deductions.
        => await _context.Batches
            .Where(b => b.ProductId == productId)
            .Sellable(Today)
            .InFefoOrder()
            .ToListAsync(cancellationToken);

    public async Task<BatchNumberClash?> FindBatchNumberClashAsync(
        Guid productId,
        string batchNumber,
        Guid? excludingBatchId = null,
        CancellationToken cancellationToken = default)
    {
        var number = batchNumber.Trim();

        // Includes inactive batches on purpose: the unique index does not exempt them, so a
        // check that ignored them would report no clash and then lose to the constraint.
        var query = _context.Batches
            .AsNoTracking()
            .Where(b => b.ProductId == productId && b.BatchNumber == number);

        if (excludingBatchId is { } excluded)
        {
            query = query.Where(b => b.Id != excluded);
        }

        return await query
            .Select(b => new BatchNumberClash(b.Id, b.QuantityInBaseUnits, b.ExpiryDate))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// The shape the stock-list projection reads. A named type rather than an anonymous one
    /// so the projection can be written across several lines with a comment on each aggregate.
    /// </summary>
    private sealed class StockRow
    {
        public Product Product { get; init; } = null!;

        public int SellableQuantity { get; init; }

        public int ExpiredQuantity { get; init; }

        public int BatchCount { get; init; }

        public DateOnly? NearestExpiry { get; init; }
    }

    private sealed class AdjustmentRow
    {
        public StockAdjustment Adjustment { get; init; } = null!;

        public Batch Batch { get; init; } = null!;

        public Product Product { get; init; } = null!;

        public string? AdjustedByName { get; init; }
    }
}
