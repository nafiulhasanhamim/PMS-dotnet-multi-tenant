using Microsoft.EntityFrameworkCore;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.Persistence.Contexts;

namespace PMS.Persistence.Services;

/// <summary>
/// The outstanding-balance calculation. See <see cref="ISupplierBalanceQueries"/> for why it
/// lives in exactly one place.
///
/// <para><b>Every method here runs three separate grouped aggregates and joins them in
/// memory.</b> That is deliberate and it is not a performance compromise — it is the shape that
/// works. Composing the three sums as correlated subqueries inside a projection over suppliers
/// reads better and fails at runtime: SQL Server rejects <em>"Cannot perform an aggregate
/// function on an expression containing an aggregate or a subquery"</em>, which this codebase has
/// now hit in Modules 6, 7 and 8. Each query below groups over one base table and aggregates a
/// plain column.</para>
///
/// <para><b>The three aggregates are also the reason the list and the export cannot diverge.</b>
/// A paginated screen passes its page of ids to <see cref="GetBalancesAsync"/>; an export passes
/// every id to the same method. One code path, one query shape — Module 8 shipped a report whose
/// streaming path worked while its paginated path 500'd, and this is the structure that prevents
/// a repeat.</para>
///
/// <para>No method takes a tenant id. Every table read implements <c>ITenantEntity</c>, so the
/// global query filter supplies the pharmacy — inside the aggregates too, which is what makes one
/// pharmacy's debts its own.</para>
/// </summary>
public sealed class SupplierBalanceQueries : ISupplierBalanceQueries
{
    private readonly ApplicationDbContext _context;

    public SupplierBalanceQueries(ApplicationDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<SupplierBalance> GetBalanceAsync(
        Guid supplierId, CancellationToken cancellationToken = default)
    {
        var balances = await GetBalancesAsync([supplierId], cancellationToken);

        return balances.TryGetValue(supplierId, out var balance) ? balance : SupplierBalance.Zero;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, SupplierBalance>> GetBalancesAsync(
        IReadOnlyCollection<Guid> supplierIds, CancellationToken cancellationToken = default)
    {
        if (supplierIds.Count == 0)
        {
            return new Dictionary<Guid, SupplierBalance>();
        }

        var ids = supplierIds.Distinct().ToList();

        var purchased = await _context.Purchases
            .AsNoTracking()
            .Where(p => ids.Contains(p.SupplierId))
            .GroupBy(p => p.SupplierId)
            .Select(g => new { SupplierId = g.Key, Total = g.Sum(p => p.TotalAmount) })
            .ToDictionaryAsync(x => x.SupplierId, x => x.Total, cancellationToken);

        // Returns reach the supplier through two navigations. A join and a group — still one
        // aggregate over a plain column, which is the part that matters.
        var returned = await _context.PurchaseReturns
            .AsNoTracking()
            .Where(r => ids.Contains(r.PurchaseLine.Purchase.SupplierId))
            .GroupBy(r => r.PurchaseLine.Purchase.SupplierId)
            .Select(g => new { SupplierId = g.Key, Total = g.Sum(r => r.ReturnAmount) })
            .ToDictionaryAsync(x => x.SupplierId, x => x.Total, cancellationToken);

        // Grouped by direction in one pass: money out, money back, and credit given up. A single
        // sum over Amount would add refunds to payments and understate what is owed by twice the
        // refund — which is exactly the kind of error a signed-amount design invites.
        //
        // A general payment has no PurchaseId and still counts here; excluding it would be the
        // single most likely way to overstate a pharmacy's debts.
        var movements = await _context.SupplierPayments
            .AsNoTracking()
            .Where(p => ids.Contains(p.SupplierId))
            .GroupBy(p => new { p.SupplierId, p.Direction })
            .Select(g => new
            {
                g.Key.SupplierId,
                g.Key.Direction,
                Total = g.Sum(p => p.Amount),
            })
            .ToListAsync(cancellationToken);

        return Combine(ids, purchased, returned, movements);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, SupplierBalance>> GetActivityAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var ids = await _context.Suppliers
            .AsNoTracking()
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, SupplierBalance>();
        }

        var purchases = _context.Purchases.AsNoTracking().AsQueryable();
        var payments = _context.SupplierPayments.AsNoTracking().AsQueryable();
        var returns = _context.PurchaseReturns.AsNoTracking().AsQueryable();

        if (from is { } start)
        {
            purchases = purchases.Where(p => p.PurchaseDate >= start);
            payments = payments.Where(p => p.PaymentDate >= start);

            // Returns carry no date of their own beyond when they were recorded, which is the
            // moment the goods went back — the same basis Module 8 uses for sales returns.
            var startUtc = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            returns = returns.Where(r => r.CreatedOnUtc >= startUtc);
        }

        if (to is { } end)
        {
            purchases = purchases.Where(p => p.PurchaseDate <= end);
            payments = payments.Where(p => p.PaymentDate <= end);

            var endUtc = end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            returns = returns.Where(r => r.CreatedOnUtc < endUtc);
        }

        var purchased = await purchases
            .GroupBy(p => p.SupplierId)
            .Select(g => new { SupplierId = g.Key, Total = g.Sum(p => p.TotalAmount) })
            .ToDictionaryAsync(x => x.SupplierId, x => x.Total, cancellationToken);

        var returnedTotals = await returns
            .GroupBy(r => r.PurchaseLine.Purchase.SupplierId)
            .Select(g => new { SupplierId = g.Key, Total = g.Sum(r => r.ReturnAmount) })
            .ToDictionaryAsync(x => x.SupplierId, x => x.Total, cancellationToken);

        var movements = await payments
            .GroupBy(p => new { p.SupplierId, p.Direction })
            .Select(g => new
            {
                g.Key.SupplierId,
                g.Key.Direction,
                Total = g.Sum(p => p.Amount),
            })
            .ToListAsync(cancellationToken);

        return Combine(ids, purchased, returnedTotals, movements);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, BillAdjustment>> GetGeneralPaymentAllocationsAsync(
        IReadOnlyCollection<Guid> supplierIds, CancellationToken cancellationToken = default)
    {
        if (supplierIds.Count == 0)
        {
            return new Dictionary<Guid, BillAdjustment>();
        }

        var ids = supplierIds.Distinct().ToList();

        // Two pools, and they move bills in opposite directions.
        //
        //   Payments  with no PurchaseId  -> cover bills that OWE something (due comes down)
        //   Refunds and write-offs        -> clear bills in CREDIT      (due comes back up)
        //
        // Purchase-linked payments are already on their purchase's AmountPaid and are excluded
        // here, or they would count twice.
        var movements = await _context.SupplierPayments
            .AsNoTracking()
            .Where(p => p.PurchaseId == null && ids.Contains(p.SupplierId))
            .GroupBy(p => new { p.SupplierId, p.Direction })
            .Select(g => new
            {
                g.Key.SupplierId,
                g.Key.Direction,
                Total = g.Sum(p => p.Amount),
            })
            .ToListAsync(cancellationToken);

        if (movements.Count == 0)
        {
            // Nothing to allocate. The common case for a pharmacy that always links its payments,
            // and worth short-circuiting before reading every bill they hold.
            return new Dictionary<Guid, BillAdjustment>();
        }

        var pools = movements
            .Where(m => m.Direction == SupplierPaymentDirection.Payment)
            .ToDictionary(m => m.SupplierId, m => m.Total);

        // Refunds and write-offs both undo a credit, so they share one pool: the bills cannot
        // tell which of the two settled them, and neither can anybody reading the page.
        var credits = movements
            .Where(m => m.Direction != SupplierPaymentDirection.Payment)
            .GroupBy(m => m.SupplierId)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Total));

        var withPool = pools.Keys.Concat(credits.Keys).Distinct().ToList();

        var bills = await _context.Purchases
            .AsNoTracking()
            .Where(p => withPool.Contains(p.SupplierId))
            .Select(p => new
            {
                p.Id,
                p.SupplierId,
                p.PurchaseDate,
                p.CreatedOnUtc,
                p.TotalAmount,
                p.AmountPaid,
            })
            .ToListAsync(cancellationToken);

        var billIds = bills.Select(b => b.Id).ToList();

        var returned = await _context.PurchaseReturns
            .AsNoTracking()
            .Where(r => billIds.Contains(r.PurchaseLine.PurchaseId))
            .GroupBy(r => r.PurchaseLine.PurchaseId)
            .Select(g => new { PurchaseId = g.Key, Total = g.Sum(r => r.ReturnAmount) })
            .ToDictionaryAsync(x => x.PurchaseId, x => x.Total, cancellationToken);

        var allocations = new Dictionary<Guid, BillAdjustment>();

        foreach (var supplier in bills.GroupBy(b => b.SupplierId))
        {
            var pool = pools.TryGetValue(supplier.Key, out var amount) ? amount : 0m;
            var credit = credits.TryGetValue(supplier.Key, out var c) ? c : 0m;

            // Oldest first, by the date on the bill and then by when it was recorded — which is
            // how a supplier applies an unallocated payment, and what the module brief specifies.
            var ordered = supplier
                .OrderBy(b => b.PurchaseDate)
                .ThenBy(b => b.CreatedOnUtc)
                .ToList();

            foreach (var bill in ordered)
            {
                var back = returned.TryGetValue(bill.Id, out var r) ? r : 0m;
                var owed = bill.TotalAmount - bill.AmountPaid - back;

                var covered = 0m;
                var settled = 0m;

                if (owed > 0m && pool > 0m)
                {
                    // A bill that owes something takes from the payment pool, capped at what it
                    // owes. A settled bill absorbs nothing: crediting one that owes nothing would
                    // push it further into credit and leave a bill that IS owed reading as unpaid.
                    covered = Math.Min(pool, owed);
                    pool -= covered;
                }
                else if (owed < 0m && credit > 0m)
                {
                    // A bill in credit is cleared by a refund or a write-off, capped at the credit
                    // it holds. This is the mirror of the line above, and it is what keeps the
                    // bills adding up to the balance once money comes back.
                    settled = Math.Min(credit, -owed);
                    credit -= settled;
                }

                if (covered > 0m || settled > 0m)
                {
                    allocations[bill.Id] = new BillAdjustment(covered, settled);
                }
            }

            // Anything left in either pool stays unallocated, which is the honest result: a
            // surplus payment is a credit on the ACCOUNT rather than on any one bill, and a refund
            // larger than the credit means the supplier handed back too much.
        }

        return allocations;
    }

    /// <summary>
    /// Stitches the three dictionaries together.
    ///
    /// <para>Every requested id gets an entry, zero where there was no activity, so no caller has
    /// to decide what an absent key means — and a supplier with no purchases shows a balance of
    /// 0.00 rather than a blank cell.</para>
    /// </summary>
    private static Dictionary<Guid, SupplierBalance> Combine(
        IEnumerable<Guid> ids,
        IReadOnlyDictionary<Guid, decimal> purchased,
        IReadOnlyDictionary<Guid, decimal> returned,
        IReadOnlyCollection<dynamic> movements)
    {
        decimal Movement(Guid supplierId, SupplierPaymentDirection direction) =>
            movements
                .Where(m => (Guid)m.SupplierId == supplierId
                    && (SupplierPaymentDirection)m.Direction == direction)
                .Select(m => (decimal)m.Total)
                .FirstOrDefault();

        var result = new Dictionary<Guid, SupplierBalance>();

        foreach (var id in ids)
        {
            result[id] = new SupplierBalance(
                purchased.TryGetValue(id, out var p) ? p : 0m,
                returned.TryGetValue(id, out var r) ? r : 0m,
                Movement(id, SupplierPaymentDirection.Payment),
                Movement(id, SupplierPaymentDirection.Refund),
                Movement(id, SupplierPaymentDirection.WriteOff));
        }

        return result;
    }
}
