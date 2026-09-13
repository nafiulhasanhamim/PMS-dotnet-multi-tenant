using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Units;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.Persistence.Contexts;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Interfaces;

namespace PMS.Persistence.Services;

/// <summary>
/// Read-side projections over this pharmacy's sales.
///
/// <para><b>There is no WHERE on TenantId anywhere in this file, and that is correct.</b>
/// <c>Sale</c>, <c>SaleLine</c> and <c>SalesReturn</c> all implement <c>ITenantEntity</c>, so
/// the global query filter adds it to every query below. Writing one by hand would be harmless
/// duplication today and misleading tomorrow — it would suggest the filter is not doing its
/// job.</para>
///
/// <para><b>Prices are read from the sale, never from the product.</b> Every figure on a
/// historical invoice comes out of <c>SaleLine</c>: the unit price, the line total, the
/// discount share. Joining to <c>Product</c> for a price would make last month's invoices
/// change when somebody re-prices a product this month, which is the single failure this module
/// was designed to prevent. The product is joined for its <em>name</em> and its unit
/// configuration, which are labels rather than money.</para>
/// </summary>
public sealed class SaleQueries : ISaleQueries
{
    private readonly ApplicationDbContext _context;
    private readonly IDateTime _clock;

    public SaleQueries(ApplicationDbContext context, IDateTime clock)
    {
        _context = context;
        _clock = clock;
    }

    private DateOnly Today => _clock.UtcDateToday();

    /// <inheritdoc />
    public async Task<IReadOnlyList<SellableProductDto>> SearchSellableAsync(
        string? search,
        bool callerMaySellAntibiotics,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var today = Today;
        var term = search?.Trim();

        // Inactive products are excluded outright rather than flagged. Deactivating a product
        // is how a pharmacy says it no longer sells the thing at all, so offering it greyed out
        // at the till would invite somebody to reactivate it mid-sale. Every other reason a
        // product cannot be sold is returned with the reason attached — see SellableStatus.
        var query = _context.Products.AsNoTracking().Where(product => product.IsActive);

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(product =>
                EF.Functions.Like(product.BrandName, $"%{term}%")
                || (product.GenericName != null
                    && EF.Functions.Like(product.GenericName, $"%{term}%")));
        }

        var rows = await query
            .OrderBy(product => product.BrandName)
            .Take(limit)
            .Select(product => new
            {
                Product = product,

                // Two sums, because "we have none" and "we have some and all of it has
                // expired" are different conversations to have with a customer. The sellable
                // one mirrors Fefo.IsSellable exactly; if that rule changes, this has to change
                // with it, which is why it is written the same way round.
                Sellable = product.Id == default
                    ? 0
                    : _context.Batches
                        .Where(batch => batch.ProductId == product.Id)
                        .Where(batch => batch.IsActive
                            && batch.QuantityInBaseUnits > 0
                            && (batch.ExpiryDate == null || batch.ExpiryDate >= today))
                        .Sum(batch => batch.QuantityInBaseUnits),

                Expired = _context.Batches
                    .Where(batch => batch.ProductId == product.Id)
                    .Where(batch => batch.IsActive
                        && batch.QuantityInBaseUnits > 0
                        && batch.ExpiryDate != null && batch.ExpiryDate < today)
                    .Sum(batch => batch.QuantityInBaseUnits),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => Describe(row.Product, row.Sellable, row.Expired, callerMaySellAntibiotics))
            .ToList();
    }

    /// <summary>
    /// Decides which of the blocking reasons applies, and words it.
    ///
    /// <para>The order is the order a cashier can act on. Prices missing comes first because it
    /// is fixable in fifteen seconds by the person standing there; the role rule comes before
    /// stock because fetching a pharmacist is a different action from waiting for a delivery;
    /// expiry comes before plain out-of-stock because it says something about what is on the
    /// shelf that the shelf itself does not.</para>
    /// </summary>
    private static SellableProductDto Describe(
        Product product, int sellable, int expired, bool callerMaySellAntibiotics)
    {
        var status = SellableStatus.Sellable;
        string? reason = null;

        if (!product.IsSetupComplete)
        {
            status = SellableStatus.SetupIncomplete;
            reason = "This product needs prices set before it can be sold.";
        }
        else if (product.IsAntibiotic && !callerMaySellAntibiotics)
        {
            status = SellableStatus.RequiresPharmacist;
            reason = "Requires a pharmacist.";
        }
        else if (sellable <= 0 && expired > 0)
        {
            status = SellableStatus.AllStockExpired;
            reason = $"All available stock of {product.BrandName} has expired. "
                + "No sale possible until new stock arrives.";
        }
        else if (sellable <= 0)
        {
            status = SellableStatus.OutOfStock;
            reason = "No stock available.";
        }

        return new SellableProductDto(
            product.Id,
            product.BrandName,
            product.GenericName,
            product.Strength,
            product.DosageForm,
            product.ProductType,
            product.IsAntibiotic,
            product.BaseUnitName,
            product.MidUnitName,
            product.LargeUnitName,
            product.BasePerMid,
            product.BaseUnitsPerLarge,
            product.PricePerBase,
            product.PricePerMid,
            product.PricePerLarge,
            sellable,
            expired,
            UnitConversion.FromBaseUnits(sellable, product),
            status,
            reason);
    }

    /// <inheritdoc />
    public async Task<GridResult<SaleListItemDto>> ListAsync(
        DateOnly? from,
        DateOnly? to,
        Guid? cashierUserId,
        SaleStatusFilter status,
        Guid? onlyCashierUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Sales.AsNoTracking();

        if (from is { } fromDate)
        {
            var start = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(sale => sale.SaleDate >= start);
        }

        if (to is { } toDate)
        {
            // Inclusive of the whole end day. A range of "1 to 7 September" that stopped at
            // midnight on the 7th would silently omit every sale made on the 7th, which is the
            // day somebody checking today's takings cares about most.
            var end = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(sale => sale.SaleDate < end);
        }

        // The restriction first, so it cannot be widened by the filter below it.
        if (onlyCashierUserId is { } restricted)
        {
            query = query.Where(sale => sale.CashierUserId == restricted);
        }
        else if (cashierUserId is { } cashier)
        {
            query = query.Where(sale => sale.CashierUserId == cashier);
        }

        query = status switch
        {
            SaleStatusFilter.Completed => query.Where(sale => sale.Status == SaleStatus.Completed),
            SaleStatusFilter.Cancelled => query.Where(sale => sale.Status == SaleStatus.Cancelled),
            _ => query,
        };

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(sale => sale.SaleDate)
            .ThenByDescending(sale => sale.InvoiceNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(sale => new
            {
                sale.Id,
                sale.InvoiceNumber,
                sale.SaleDate,
                sale.CashierUserId,
                sale.Subtotal,
                sale.DiscountAmount,
                sale.NetTotal,
                sale.Status,

                // Distinct products, not sale lines: a FEFO split across two batches is one
                // thing the customer bought, and a list column reading "3 items" for a
                // two-item sale would look like a bug to the person who rang it up.
                ItemCount = _context.SaleLines
                    .Where(line => line.SaleId == sale.Id)
                    .Select(line => line.ProductId)
                    .Distinct()
                    .Count(),

                HasReturns = _context.SalesReturns
                    .Any(ret => ret.SaleLine.SaleId == sale.Id),

                // An explicit left join to the global Users table rather than a mapped
                // navigation. Users has no TenantId — one person can work at two pharmacies —
                // so this is a tenant-scoped row reaching into unfiltered data, and writing it
                // out keeps that crossing visible instead of burying it in the model.
                CashierName = _context.Users
                    .Where(user => user.Id == sale.CashierUserId)
                    .Select(user => user.FullName)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new SaleListItemDto(
            row.Id,
            row.InvoiceNumber,
            row.SaleDate,
            row.CashierUserId,
            row.CashierName,
            row.ItemCount,
            row.Subtotal,
            row.DiscountAmount,
            row.NetTotal,
            row.Status,
            row.HasReturns))
            .ToList();

        return GridResult<SaleListItemDto>.Create(items, total, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<SaleDetailDto?> FindAsync(
        Guid saleId, CancellationToken cancellationToken = default)
    {
        var sale = await _context.Sales
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == saleId, cancellationToken);

        if (sale is null)
        {
            return null;
        }

        var lines = await LoadLinesAsync(saleId, cancellationToken);

        var cashierName = await NameOfAsync(sale.CashierUserId, cancellationToken);
        var cancelledByName = sale.CancelledByUserId is { } cancelledBy
            ? await NameOfAsync(cancelledBy, cancellationToken)
            : null;

        var lineDtos = lines.Select(row => new SaleLineDto(
            row.Line.Id,
            row.Line.ProductId,
            row.Product.BrandName,
            row.Product.Strength,
            row.Line.BatchId,
            row.BatchNumber,
            row.BatchExpiryDate,
            row.Line.QuantityInBaseUnits,
            row.Line.UnitSold,
            UnitConversion.Describe(row.Line.UnitSold, row.Product),
            row.Product.BaseUnitName,
            row.Line.UnitSalePrice,
            row.Line.LineTotal,
            row.Line.DiscountShare,
            row.Line.NetLineTotal,
            row.Returned,
            row.Refunded))
            .ToList();

        return new SaleDetailDto(
            sale.Id,
            sale.InvoiceNumber,
            sale.SaleDate,
            sale.CashierUserId,
            cashierName,
            sale.Subtotal,
            sale.DiscountType,
            sale.DiscountValue,
            sale.DiscountAmount,
            sale.NetTotal,
            sale.CashReceived,
            sale.ChangeGiven,
            sale.Status,
            sale.CancelledReason,
            cancelledByName,
            sale.CancelledAt,
            sale.CustomerName,
            sale.CustomerPhone,
            sale.HasPrescription
                ? new PrescriptionDto(
                    sale.PatientName,
                    sale.PatientPhone,
                    sale.DoctorName,
                    sale.PrescriptionNumber,
                    sale.PrescriptionDate,
                    sale.PrescriptionVerified)
                : null,
            GroupForInvoice(lines),
            lineDtos,
            lines.Sum(row => row.Refunded));
    }

    /// <summary>
    /// Merges the FEFO split back into what the customer bought.
    ///
    /// <para>Grouped on product and unit level. The sale price is copied from the product rather
    /// than from the batch, so every line of a split carries the same <c>UnitSalePrice</c> —
    /// which is what makes this merge arithmetic rather than an average. Quantities and totals
    /// are summed; no money figure is recomputed, so the invoice cannot drift from the sale.
    /// </para>
    ///
    /// <para>The same product sold at two levels on one bill — two strips and three loose
    /// tablets — stays two rows, because that is what the customer asked for and what the
    /// prices differ on.</para>
    /// </summary>
    private static List<InvoiceLineDto> GroupForInvoice(IReadOnlyList<LineRow> lines) =>
        lines
            .GroupBy(row => new { row.Line.ProductId, row.Line.UnitSold })
            .Select(group =>
            {
                var first = group.First();
                var baseUnits = group.Sum(row => row.Line.QuantityInBaseUnits);
                var perSoldUnit = UnitConversion.BaseUnitsIn(first.Line.UnitSold, first.Product);

                return new InvoiceLineDto(
                    first.Line.ProductId,
                    first.Product.BrandName,
                    first.Product.Strength,
                    first.Product.DosageForm,
                    first.Product.IsAntibiotic,

                    // Back into the unit the customer bought in. Exact by construction: the
                    // total is a whole number of sold units, because that is what was asked
                    // for before FEFO divided it up.
                    perSoldUnit > 0 ? (decimal)baseUnits / perSoldUnit : baseUnits,
                    first.Line.UnitSold,
                    UnitConversion.Describe(first.Line.UnitSold, first.Product),
                    baseUnits,
                    first.Line.UnitSalePrice,
                    group.Sum(row => row.Line.LineTotal),
                    group.Sum(row => row.Line.DiscountShare),
                    group.Sum(row => row.Line.NetLineTotal),
                    group.Sum(row => row.Returned),
                    group.Count());
            })
            .OrderBy(line => line.BrandName)
            .ToList();

    /// <inheritdoc />
    public async Task<ReturnableSaleDto?> FindReturnableAsync(
        Guid saleId, CancellationToken cancellationToken = default)
    {
        var sale = await _context.Sales
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == saleId, cancellationToken);

        if (sale is null)
        {
            return null;
        }

        var lines = await LoadLinesAsync(saleId, cancellationToken);

        var lineDtos = lines.Select(row =>
        {
            var returnable = row.Line.QuantityInBaseUnits - row.Returned;

            return new ReturnableLineDto(
                row.Line.Id,
                row.Line.ProductId,
                row.Product.BrandName,
                row.Product.Strength,
                row.BatchNumber,
                row.Line.QuantityInBaseUnits,
                row.Returned,
                returnable,
                row.Line.UnitSold,
                row.Product.BaseUnitName,
                row.Product.MidUnitName,
                row.Product.LargeUnitName,
                row.Product.BasePerMid,
                row.Product.BaseUnitsPerLarge,
                row.Line.UnitSalePrice,
                row.Line.NetLineTotal,
                row.Refunded,

                // Computed with the same function the command uses, so the figure shown before
                // confirming is the figure that gets paid. Returning everything outstanding
                // completes the line, so this is the remainder — which is what makes a
                // sequence of partial returns add up to the net line total exactly.
                Domain.Billing.SaleMath.RefundFor(
                    returnable,
                    row.Line.QuantityInBaseUnits,
                    row.Line.NetLineTotal,
                    row.Returned,
                    row.Refunded),
                UnitConversion.FromBaseUnits(row.Line.QuantityInBaseUnits, row.Product),
                UnitConversion.FromBaseUnits(returnable, row.Product));
        })
            .OrderBy(line => line.BrandName)
            .ToList();

        return new ReturnableSaleDto(
            sale.Id,
            sale.InvoiceNumber,
            sale.SaleDate,
            sale.Status,
            sale.NetTotal,
            lines.Sum(row => row.Refunded),
            lineDtos);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CashierOptionDto>> ListCashiersAsync(
        CancellationToken cancellationToken = default)
    {
        var grouped = await _context.Sales
            .AsNoTracking()
            .GroupBy(sale => sale.CashierUserId)
            .Select(group => new { UserId = group.Key, SaleCount = group.Count() })
            .ToListAsync(cancellationToken);

        var ids = grouped.Select(row => row.UserId).ToList();

        // The global Users table again, and again as an explicit read rather than a navigation.
        var names = await _context.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName })
            .ToListAsync(cancellationToken);

        var nameById = names.ToDictionary(row => row.Id, row => row.FullName);

        return grouped
            .Select(row => new CashierOptionDto(
                row.UserId,

                // A cashier whose account has since been deleted still has sales, and hiding
                // them would hide those sales from the filter. Naming the id is ugly and
                // honest.
                nameById.TryGetValue(row.UserId, out var name) ? name : "(unknown user)",
                row.SaleCount))
            .OrderBy(option => option.Name)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SaleStatus?> FindStatusAsync(
        Guid saleId, CancellationToken cancellationToken = default)
    {
        var statuses = await _context.Sales
            .AsNoTracking()
            .Where(sale => sale.Id == saleId)
            .Select(sale => sale.Status)
            .ToListAsync(cancellationToken);

        return statuses.Count > 0 ? statuses[0] : null;
    }

    /// <summary>
    /// One sale's lines with the product, the batch label and the returns already summed.
    ///
    /// <para>Shared by the invoice and the return screen, which need exactly the same rows for
    /// different reasons — and would otherwise be two projections that could disagree about how
    /// much of a line has come back.</para>
    /// </summary>
    private async Task<List<LineRow>> LoadLinesAsync(
        Guid saleId, CancellationToken cancellationToken)
    {
        var rows = await _context.SaleLines
            .AsNoTracking()
            .Where(line => line.SaleId == saleId)
            .Select(line => new
            {
                Line = line,
                Product = line.Product,
                BatchNumber = line.Batch.BatchNumber,
                BatchExpiryDate = line.Batch.ExpiryDate,
                Returned = line.Returns.Sum(ret => ret.QuantityReturnedInBaseUnits),
                Refunded = line.Returns.Sum(ret => ret.RefundAmount),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new LineRow
            {
                Line = row.Line,
                Product = row.Product,
                BatchNumber = row.BatchNumber,
                BatchExpiryDate = row.BatchExpiryDate,
                Returned = row.Returned,
                Refunded = row.Refunded,
            })
            .OrderBy(row => row.Product.BrandName)
            .ThenBy(row => row.BatchNumber)
            .ToList();
    }

    private async Task<string?> NameOfAsync(Guid userId, CancellationToken cancellationToken)
    {
        var names = await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.FullName)
            .ToListAsync(cancellationToken);

        return names.Count > 0 ? names[0] : null;
    }

    /// <summary>One sale line with everything the two read paths need alongside it.</summary>
    private sealed class LineRow
    {
        public SaleLine Line { get; init; } = null!;

        public Product Product { get; init; } = null!;

        public string BatchNumber { get; init; } = null!;

        public DateOnly? BatchExpiryDate { get; init; }

        public int Returned { get; init; }

        public decimal Refunded { get; init; }
    }
}
