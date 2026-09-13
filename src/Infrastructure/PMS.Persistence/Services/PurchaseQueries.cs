using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Purchasing;
using PMS.Application.Common.Units;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Persistence.Contexts;
using PMS.SharedKernel.Grid;

namespace PMS.Persistence.Services;

/// <summary>
/// Reads over purchases, their lines, and what can still go back.
///
/// <para><b>Due and status are never stored and never recomputed inline.</b> Both come from
/// <c>PurchaseMath</c>, and both depend on returns booked against a purchase's lines — which is
/// why every method here fetches the purchases it needs and then the returns for exactly those
/// ids, rather than projecting a correlated aggregate per row. That shape is what SQL Server
/// refuses to count or aggregate over.</para>
///
/// <para>Formatted quantities are produced in memory after materialising, because
/// <c>UnitConversion.FromBaseUnits</c> needs the <c>Product</c> entity and cannot be translated
/// to SQL. The product is carried into the projection for that purpose.</para>
/// </summary>
public sealed class PurchaseQueries : IPurchaseQueries
{
    private readonly ApplicationDbContext _context;
    private readonly ISupplierBalanceQueries _balances;

    public PurchaseQueries(ApplicationDbContext context, ISupplierBalanceQueries balances)
    {
        _context = context;
        _balances = balances;
    }

    /// <inheritdoc />
    public async Task<GridResult<PurchaseListItemDto>> GetPurchasesAsync(
        Guid? supplierId,
        DateOnly? from,
        DateOnly? to,
        PurchaseStatusFilter status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var purchases = _context.Purchases.AsNoTracking();

        if (supplierId is { } supplier)
        {
            purchases = purchases.Where(p => p.SupplierId == supplier);
        }

        if (from is { } start)
        {
            purchases = purchases.Where(p => p.PurchaseDate >= start);
        }

        if (to is { } end)
        {
            purchases = purchases.Where(p => p.PurchaseDate <= end);
        }

        var ordered = purchases
            .OrderByDescending(p => p.PurchaseDate)
            .ThenByDescending(p => p.CreatedOnUtc);

        // ── The status filter costs a full read, and that is the honest way round ──────────
        //
        // A purchase's status is not a column: it depends on returns booked against its lines,
        // which live in another table. Filtering in SQL would mean a correlated aggregate in the
        // WHERE — the shape that has failed at runtime three times here — and storing the status
        // would mean a column that goes stale the moment a return is recorded.
        //
        // So when a status filter is applied, the matching purchases are read, their returns
        // fetched in one grouped query, the status computed, and the page taken in memory. The
        // set is bounded by the supplier and date filters the screen also offers. With no status
        // filter — the default — paging happens in the database as usual.
        if (status == PurchaseStatusFilter.All)
        {
            var total = await purchases.CountAsync(cancellationToken);

            var page_ = await ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(Projection)
                .ToListAsync(cancellationToken);

            var returned = await ReturnedByPurchaseAsync(
                page_.Select(r => r.Id).ToList(), cancellationToken);

            // Asked for every supplier ON THIS PAGE. The allocation is oldest-first within a
            // supplier, so it cannot be computed from the page's bills alone.
            var allocations = await _balances.GetGeneralPaymentAllocationsAsync(
                page_.Select(r => r.SupplierId).Distinct().ToList(), cancellationToken);

            return GridResult<PurchaseListItemDto>.Create(
                page_.Select(r => ToListItem(r, returned, allocations)).ToList(),
                total, page, pageSize);
        }

        var all = await ordered.Select(Projection).ToListAsync(cancellationToken);

        var allReturned = await ReturnedByPurchaseAsync(
            all.Select(r => r.Id).ToList(), cancellationToken);

        var allAllocations = await _balances.GetGeneralPaymentAllocationsAsync(
            all.Select(r => r.SupplierId).Distinct().ToList(), cancellationToken);

        var wanted = status switch
        {
            PurchaseStatusFilter.Unpaid => PurchasePaymentStatus.Unpaid,
            PurchaseStatusFilter.PartiallyPaid => PurchasePaymentStatus.PartiallyPaid,
            _ => PurchasePaymentStatus.Paid,
        };

        var filtered = all
            .Select(r => ToListItem(r, allReturned, allAllocations))
            .Where(r => r.Status == wanted)
            .ToList();

        return GridResult<PurchaseListItemDto>.Create(
            filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            filtered.Count, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<PurchaseDetailDto?> GetPurchaseAsync(
        Guid purchaseId, CancellationToken cancellationToken = default)
    {
        var purchase = await _context.Purchases
            .AsNoTracking()
            .Where(p => p.Id == purchaseId)
            .Select(p => new
            {
                p.Id,
                p.PurchaseNumber,
                p.SupplierId,
                SupplierName = p.Supplier.Name,
                SupplierPhone = p.Supplier.Phone,
                p.PurchaseDate,
                p.TotalAmount,
                p.AmountPaid,
                p.Notes,
                p.CreatedByUserId,
                p.CreatedOnUtc,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (purchase is null)
        {
            return null;
        }

        var lineRows = await _context.PurchaseLines
            .AsNoTracking()
            .Where(l => l.PurchaseId == purchaseId)
            .Select(l => new
            {
                l.Id,
                l.ProductId,
                Product = l.Product,
                l.BatchId,
                BatchNumber = l.Batch.BatchNumber,
                ExpiryDate = l.Batch.ExpiryDate,
                BatchQuantity = l.Batch.QuantityInBaseUnits,
                l.QuantityInBaseUnits,
                l.PurchasePricePerBaseUnit,
                l.LineTotal,
            })
            .ToListAsync(cancellationToken);

        var returnedByLine = await ReturnedQuantityByLineAsync(
            lineRows.Select(l => l.Id).ToList(), cancellationToken);

        var lines = lineRows
            .Select(l =>
            {
                var back = returnedByLine.TryGetValue(l.Id, out var q) ? q : 0;

                return new PurchaseLineDto(
                    l.Id, l.ProductId, l.Product.BrandName, l.Product.GenericName,
                    l.Product.ProductType, l.BatchId, l.BatchNumber, l.ExpiryDate,
                    l.QuantityInBaseUnits,
                    UnitConversion.FromBaseUnits(l.QuantityInBaseUnits, l.Product),
                    l.PurchasePricePerBaseUnit, l.Product.BaseUnitName, l.LineTotal,
                    back,
                    back > 0 ? UnitConversion.FromBaseUnits(back, l.Product) : null,
                    l.BatchQuantity);
            })
            .OrderBy(l => l.BrandName)
            .ToList();

        var paymentRows = await _context.SupplierPayments
            .AsNoTracking()
            .Where(p => p.PurchaseId == purchaseId)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedOnUtc)
            .Select(p => new
            {
                p.Id,
                p.PaymentDate,
                p.Amount,
                p.PurchaseId,
                p.Direction,
                p.PaymentMethod,
                p.Notes,
                p.RecordedByUserId,
            })
            .ToListAsync(cancellationToken);

        var userIds = paymentRows.Select(p => p.RecordedByUserId)
            .Append(purchase.CreatedByUserId)
            .Distinct()
            .ToList();

        var names = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);

        var payments = paymentRows
            .Select(p => new SupplierPaymentRowDto(
                p.Id, p.PaymentDate, p.Amount, p.Direction, p.PurchaseId, purchase.PurchaseNumber,
                p.PaymentMethod, p.Notes,
                names.TryGetValue(p.RecordedByUserId, out var who) ? who : null))
            .ToList();

        var returnedAmount = lineRows.Sum(
            l => (returnedByLine.TryGetValue(l.Id, out var q) ? q : 0) * l.PurchasePricePerBaseUnit);

        var allocations = await _balances.GetGeneralPaymentAllocationsAsync(
            [purchase.SupplierId], cancellationToken);

        var adjustment = allocations.TryGetValue(purchase.Id, out var a)
            ? a
            : BillAdjustment.None;

        return new PurchaseDetailDto(
            purchase.Id, purchase.PurchaseNumber, purchase.SupplierId, purchase.SupplierName,
            purchase.SupplierPhone, purchase.PurchaseDate, purchase.TotalAmount,
            purchase.AmountPaid, returnedAmount,
            adjustment.GeneralPaymentApplied, adjustment.CreditSettled,
            PurchaseMath.Due(purchase.TotalAmount, purchase.AmountPaid, returnedAmount,
                adjustment.GeneralPaymentApplied, adjustment.CreditSettled),
            PurchaseMath.StatusFor(purchase.TotalAmount, purchase.AmountPaid, returnedAmount,
                adjustment.GeneralPaymentApplied, adjustment.CreditSettled),
            purchase.Notes,
            names.TryGetValue(purchase.CreatedByUserId, out var creator) ? creator : null,
            purchase.CreatedOnUtc,
            lines, payments);
    }

    /// <inheritdoc />
    public async Task<ReturnablePurchaseDto?> GetReturnableLinesAsync(
        Guid purchaseId, CancellationToken cancellationToken = default)
    {
        var purchase = await _context.Purchases
            .AsNoTracking()
            .Where(p => p.Id == purchaseId)
            .Select(p => new
            {
                p.Id,
                p.PurchaseNumber,
                p.SupplierId,
                SupplierName = p.Supplier.Name,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (purchase is null)
        {
            return null;
        }

        var lineRows = await _context.PurchaseLines
            .AsNoTracking()
            .Where(l => l.PurchaseId == purchaseId)
            .Select(l => new
            {
                l.Id,
                l.ProductId,
                Product = l.Product,
                l.BatchId,
                BatchNumber = l.Batch.BatchNumber,
                ExpiryDate = l.Batch.ExpiryDate,
                BatchQuantity = l.Batch.QuantityInBaseUnits,
                l.QuantityInBaseUnits,
                l.PurchasePricePerBaseUnit,
            })
            .ToListAsync(cancellationToken);

        var returnedByLine = await ReturnedQuantityByLineAsync(
            lineRows.Select(l => l.Id).ToList(), cancellationToken);

        var lines = lineRows
            .Select(l =>
            {
                var already = returnedByLine.TryGetValue(l.Id, out var q) ? q : 0;

                var returnable = PurchaseMath.Returnable(
                    l.QuantityInBaseUnits, already, l.BatchQuantity);

                return new ReturnablePurchaseLineDto(
                    l.Id, l.ProductId, l.Product.BrandName, l.Product.GenericName,
                    l.BatchId, l.BatchNumber, l.ExpiryDate,
                    l.QuantityInBaseUnits,
                    UnitConversion.FromBaseUnits(l.QuantityInBaseUnits, l.Product),
                    already, l.BatchQuantity, returnable,
                    UnitConversion.FromBaseUnits(returnable, l.Product),
                    l.PurchasePricePerBaseUnit,
                    l.Product.BaseUnitName, l.Product.MidUnitName, l.Product.LargeUnitName,
                    l.Product.BasePerMid, l.Product.MidPerLarge,
                    PurchaseMath.CappedByStock(l.QuantityInBaseUnits, already, l.BatchQuantity));
            })
            // Returnable lines first, so the ones that can be acted on are at the top; the rest
            // stay on the page, disabled, rather than vanishing.
            .OrderByDescending(l => l.CanReturn)
            .ThenBy(l => l.BrandName)
            .ToList();

        return new ReturnablePurchaseDto(
            purchase.Id, purchase.PurchaseNumber, purchase.SupplierId, purchase.SupplierName,
            lines);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, PurchaseOriginDto>> GetPurchaseOriginsAsync(
        IReadOnlyCollection<Guid> batchIds, CancellationToken cancellationToken = default)
    {
        if (batchIds.Count == 0)
        {
            return new Dictionary<Guid, PurchaseOriginDto>();
        }

        var ids = batchIds.Distinct().ToList();

        var rows = await _context.PurchaseLines
            .AsNoTracking()
            .Where(l => ids.Contains(l.BatchId))
            .Select(l => new
            {
                l.BatchId,
                l.Id,
                l.PurchaseId,
                PurchaseNumber = l.Purchase.PurchaseNumber,
                l.Purchase.SupplierId,
                SupplierName = l.Purchase.Supplier.Name,
            })
            .ToListAsync(cancellationToken);

        // A batch has at most one purchase line — a purchase creates exactly one batch per line —
        // but grouping rather than ToDictionary means a hand-edited database cannot throw here.
        return rows
            .GroupBy(r => r.BatchId)
            .ToDictionary(
                g => g.Key,
                g => new PurchaseOriginDto(
                    g.First().PurchaseId, g.First().PurchaseNumber, g.First().Id,
                    g.First().SupplierId, g.First().SupplierName));
    }

    // ── Shared shapes ────────────────────────────────────────────────────────────────────

    /// <summary>The list projection, shared by the paged and the filtered paths.</summary>
    private static System.Linq.Expressions.Expression<Func<Purchase, PurchaseRow>> Projection =>
        p => new PurchaseRow
        {
            Id = p.Id,
            PurchaseNumber = p.PurchaseNumber,
            SupplierId = p.SupplierId,
            SupplierName = p.Supplier.Name,
            PurchaseDate = p.PurchaseDate,
            TotalAmount = p.TotalAmount,
            AmountPaid = p.AmountPaid,
            LineCount = p.Lines.Count,
        };

    private static PurchaseListItemDto ToListItem(
        PurchaseRow row,
        IReadOnlyDictionary<Guid, decimal> returned,
        IReadOnlyDictionary<Guid, BillAdjustment> allocations)
    {
        var back = returned.TryGetValue(row.Id, out var amount) ? amount : 0m;

        var adjustment = allocations.TryGetValue(row.Id, out var a) ? a : BillAdjustment.None;

        return new PurchaseListItemDto(
            row.Id, row.PurchaseNumber, row.SupplierId, row.SupplierName, row.PurchaseDate,
            row.TotalAmount, row.AmountPaid, back,
            adjustment.GeneralPaymentApplied, adjustment.CreditSettled,
            PurchaseMath.Due(row.TotalAmount, row.AmountPaid, back,
                adjustment.GeneralPaymentApplied, adjustment.CreditSettled),
            PurchaseMath.StatusFor(row.TotalAmount, row.AmountPaid, back,
                adjustment.GeneralPaymentApplied, adjustment.CreditSettled),
            row.LineCount);
    }

    private async Task<Dictionary<Guid, decimal>> ReturnedByPurchaseAsync(
        IReadOnlyCollection<Guid> purchaseIds, CancellationToken cancellationToken)
    {
        if (purchaseIds.Count == 0)
        {
            return [];
        }

        return await _context.PurchaseReturns
            .AsNoTracking()
            .Where(r => purchaseIds.Contains(r.PurchaseLine.PurchaseId))
            .GroupBy(r => r.PurchaseLine.PurchaseId)
            .Select(g => new { PurchaseId = g.Key, Total = g.Sum(r => r.ReturnAmount) })
            .ToDictionaryAsync(x => x.PurchaseId, x => x.Total, cancellationToken);
    }

    /// <summary>Quantity already returned per line, in base units.</summary>
    private async Task<Dictionary<Guid, int>> ReturnedQuantityByLineAsync(
        IReadOnlyCollection<Guid> lineIds, CancellationToken cancellationToken)
    {
        if (lineIds.Count == 0)
        {
            return [];
        }

        return await _context.PurchaseReturns
            .AsNoTracking()
            .Where(r => lineIds.Contains(r.PurchaseLineId))
            .GroupBy(r => r.PurchaseLineId)
            .Select(g => new
            {
                LineId = g.Key,
                Quantity = g.Sum(r => r.QuantityInBaseUnits),
            })
            .ToDictionaryAsync(x => x.LineId, x => x.Quantity, cancellationToken);
    }

    private sealed class PurchaseRow
    {
        public Guid Id { get; init; }

        public string PurchaseNumber { get; init; } = null!;

        public Guid SupplierId { get; init; }

        public string SupplierName { get; init; } = null!;

        public DateOnly PurchaseDate { get; init; }

        public decimal TotalAmount { get; init; }

        public decimal AmountPaid { get; init; }

        public int LineCount { get; init; }
    }
}
