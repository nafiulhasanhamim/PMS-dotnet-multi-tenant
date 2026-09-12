using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Purchasing;
using PMS.Application.Interfaces;
using PMS.Persistence.Contexts;
using PMS.SharedKernel.Grid;

namespace PMS.Persistence.Services;

/// <summary>
/// Reads over suppliers, their bills and their payments.
///
/// <para><b>Balances come from <c>ISupplierBalanceQueries</c>, always.</b> Nothing here subtracts
/// payments from purchases itself. The list pages the suppliers first and then asks for balances
/// for exactly that page's ids — two round-trips rather than one projection, because a balance
/// composed as correlated subqueries inside a projection is the shape SQL Server refuses to
/// aggregate or count over, and it is the shape that has failed at runtime three times in this
/// codebase.</para>
///
/// <para>No WHERE on TenantId anywhere except on <c>Users</c>, and that is correct: everything
/// else is an <c>ITenantEntity</c> and the global query filter supplies the pharmacy.</para>
/// </summary>
public sealed class SupplierQueries : ISupplierQueries
{
    private readonly ApplicationDbContext _context;
    private readonly ISupplierBalanceQueries _balances;

    public SupplierQueries(ApplicationDbContext context, ISupplierBalanceQueries balances)
    {
        _context = context;
        _balances = balances;
    }

    /// <inheritdoc />
    public async Task<GridResult<SupplierListItemDto>> GetSuppliersAsync(
        string? search,
        SupplierStatusFilter status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var suppliers = _context.Suppliers.AsNoTracking();

        suppliers = status switch
        {
            SupplierStatusFilter.Active => suppliers.Where(s => s.IsActive),
            SupplierStatusFilter.Inactive => suppliers.Where(s => !s.IsActive),
            _ => suppliers,
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            // Name or phone. A pharmacy looks a distributor up by whichever they can remember,
            // and the phone number is often the one written on the delivery note.
            suppliers = suppliers.Where(s => s.Name.Contains(term) || s.Phone.Contains(term));
        }

        // Counted over the filtered entities, not over a projection carrying subqueries.
        var total = await suppliers.CountAsync(cancellationToken);

        var rows = await suppliers
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.ContactPerson,
                s.Phone,
                s.Company,
                s.IsActive,
            })
            .ToListAsync(cancellationToken);

        // One extra round-trip for the whole page, rather than three correlated aggregates per
        // row. See the class remarks.
        var balances = await _balances.GetBalancesAsync(
            rows.Select(r => r.Id).ToList(), cancellationToken);

        return GridResult<SupplierListItemDto>.Create(
            rows.Select(r => new SupplierListItemDto(
                    r.Id, r.Name, r.ContactPerson, r.Phone, r.Company, r.IsActive,
                    balances.TryGetValue(r.Id, out var b) ? b : SupplierBalance.Zero))
                .ToList(),
            total, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<SupplierDetailDto?> GetSupplierAsync(
        Guid supplierId, CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
            .AsNoTracking()
            .Where(s => s.Id == supplierId)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Phone,
                s.ContactPerson,
                s.Email,
                s.Address,
                s.Company,
                s.IsActive,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (supplier is null)
        {
            // The tenant filter did this, not an authorisation check. Another pharmacy's supplier
            // id is genuinely not found here, and the 404 that follows is the truth.
            return null;
        }

        var balance = await _balances.GetBalanceAsync(supplierId, cancellationToken);

        var purchaseCount = await _context.Purchases
            .AsNoTracking()
            .CountAsync(p => p.SupplierId == supplierId, cancellationToken);

        var paymentCount = await _context.SupplierPayments
            .AsNoTracking()
            .CountAsync(p => p.SupplierId == supplierId, cancellationToken);

        return new SupplierDetailDto(
            supplier.Id, supplier.Name, supplier.Phone, supplier.ContactPerson, supplier.Email,
            supplier.Address, supplier.Company, supplier.IsActive,
            balance, purchaseCount, paymentCount);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SupplierOptionDto>> GetSupplierOptionsAsync(
        CancellationToken cancellationToken = default) =>
        await _context.Suppliers
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SupplierOptionDto(s.Id, s.Name, s.Company, s.Phone))
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<GridResult<SupplierPurchaseRowDto>> GetSupplierPurchasesAsync(
        Guid supplierId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var purchases = _context.Purchases
            .AsNoTracking()
            .Where(p => p.SupplierId == supplierId);

        var total = await purchases.CountAsync(cancellationToken);

        var rows = await purchases
            // Newest first: a supplier page is opened to see what is outstanding now, and the
            // most recent bill is almost always the reason somebody is looking.
            .OrderByDescending(p => p.PurchaseDate)
            .ThenByDescending(p => p.CreatedOnUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.PurchaseNumber,
                p.PurchaseDate,
                p.TotalAmount,
                p.AmountPaid,
                LineCount = p.Lines.Count,
            })
            .ToListAsync(cancellationToken);

        var returned = await ReturnedByPurchaseAsync(
            rows.Select(r => r.Id).ToList(), cancellationToken);

        // Computed across ALL this supplier's bills, not just this page: the allocation is
        // oldest-first, so a page-two bill's share depends on what page one absorbed.
        var allocations = await _balances.GetGeneralPaymentAllocationsAsync(
            [supplierId], cancellationToken);

        return GridResult<SupplierPurchaseRowDto>.Create(
            rows.Select(r => ToRow(r.Id, r.PurchaseNumber, r.PurchaseDate, r.TotalAmount,
                    r.AmountPaid, r.LineCount, returned, allocations))
                .ToList(),
            total, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<GridResult<SupplierPaymentRowDto>> GetSupplierPaymentsAsync(
        Guid supplierId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var payments = _context.SupplierPayments
            .AsNoTracking()
            .Where(p => p.SupplierId == supplierId);

        var total = await payments.CountAsync(cancellationToken);

        var rows = await payments
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedOnUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.PaymentDate,
                p.Amount,
                p.PurchaseId,

                // Null for a general payment, which is what the screens render as
                // "General payment" rather than leaving blank.
                PurchaseNumber = p.Purchase != null ? p.Purchase.PurchaseNumber : null,
                p.Direction,
                p.PaymentMethod,
                p.Notes,
                p.RecordedByUserId,
            })
            .ToListAsync(cancellationToken);

        var names = await UserNamesAsync(
            rows.Select(r => r.RecordedByUserId).ToList(), cancellationToken);

        return GridResult<SupplierPaymentRowDto>.Create(
            rows.Select(r => new SupplierPaymentRowDto(
                    r.Id, r.PaymentDate, r.Amount, r.Direction, r.PurchaseId, r.PurchaseNumber,
                    r.PaymentMethod, r.Notes,
                    names.TryGetValue(r.RecordedByUserId, out var name) ? name : null))
                .ToList(),
            total, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SupplierPurchaseRowDto>> GetUnsettledPurchasesAsync(
        Guid supplierId, CancellationToken cancellationToken = default)
    {
        var rows = await _context.Purchases
            .AsNoTracking()
            .Where(p => p.SupplierId == supplierId)
            .OrderBy(p => p.PurchaseDate)
            .ThenBy(p => p.CreatedOnUtc)
            .Select(p => new
            {
                p.Id,
                p.PurchaseNumber,
                p.PurchaseDate,
                p.TotalAmount,
                p.AmountPaid,
                LineCount = p.Lines.Count,
            })
            .ToListAsync(cancellationToken);

        var returned = await ReturnedByPurchaseAsync(
            rows.Select(r => r.Id).ToList(), cancellationToken);

        var allocations = await _balances.GetGeneralPaymentAllocationsAsync(
            [supplierId], cancellationToken);

        // Filtered here rather than in SQL because the due depends on returns, which are not a
        // column on the purchase. Bounded by one supplier's bills, so the set is small.
        //
        // A bill already covered by general payments is omitted, like any other settled one: it
        // owes nothing, and offering it would invite a payment against a bill that needs none.
        // When every bill is covered, only "General payment" remains — which is correct.
        return rows
            .Select(r => ToRow(r.Id, r.PurchaseNumber, r.PurchaseDate, r.TotalAmount,
                r.AmountPaid, r.LineCount, returned, allocations))
            .Where(r => r.Status != PurchasePaymentStatus.Paid)
            .ToList();
    }

    /// <summary>
    /// One purchase row, with its due and status from the one place that defines them.
    /// </summary>
    private static SupplierPurchaseRowDto ToRow(
        Guid id,
        string purchaseNumber,
        DateOnly purchaseDate,
        decimal totalAmount,
        decimal amountPaid,
        int lineCount,
        IReadOnlyDictionary<Guid, decimal> returned,
        IReadOnlyDictionary<Guid, BillAdjustment> allocations)
    {
        var back = returned.TryGetValue(id, out var amount) ? amount : 0m;

        var adjustment = allocations.TryGetValue(id, out var a) ? a : BillAdjustment.None;

        return new SupplierPurchaseRowDto(
            id, purchaseNumber, purchaseDate, totalAmount, amountPaid, back,
            adjustment.GeneralPaymentApplied, adjustment.CreditSettled,
            PurchaseMath.Due(totalAmount, amountPaid, back,
                adjustment.GeneralPaymentApplied, adjustment.CreditSettled),
            PurchaseMath.StatusFor(totalAmount, amountPaid, back,
                adjustment.GeneralPaymentApplied, adjustment.CreditSettled),
            lineCount);
    }

    /// <summary>
    /// Returns booked against each of these purchases, keyed by purchase id.
    ///
    /// <para>A grouped aggregate over one base table — no correlated subquery, so nothing here
    /// can be asked to aggregate over an aggregate.</para>
    /// </summary>
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

    /// <summary>
    /// Display names for a set of users.
    ///
    /// <para><c>Users</c> is the global identity table and is not an <c>ITenantEntity</c>, so
    /// reaching into it is an explicit lookup rather than something the query filter handles.</para>
    /// </summary>
    private async Task<Dictionary<Guid, string>> UserNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var ids = userIds.Distinct().ToList();

        return await _context.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
    }
}
