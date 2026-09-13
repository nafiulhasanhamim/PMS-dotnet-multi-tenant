using Microsoft.EntityFrameworkCore;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.Persistence.Contexts;

namespace PMS.Persistence.Services;

/// <summary>
/// What it cost to keep the doors open in a period. See <see cref="IOperatingExpenses"/>.
///
/// <para>Module 8 shipped this class returning zero, with the monthly report saying so on screen.
/// Module 9 replaced the body and deleted the note. No report changed — which was the point of
/// putting the seam here in the first place.</para>
///
/// <para><b>Two sums, and BOTH are needed. Getting this wrong is the easiest mistake in the
/// module.</b></para>
///
/// <list type="number">
///   <item>
///     <description><b>Paid salary entries, by <c>PaymentDate</c>.</b> An unpaid entry is not an
///     expense, it is a plan. And the date is the day the money left, not the month the salary was
///     for: an August salary paid on 2 September belongs to September, exactly as Module 8
///     attributes a return to the day the goods came back rather than the day of the original
///     sale.</description>
///   </item>
///   <item>
///     <description><b>Salary advances, by <c>AdvanceDate</c>, settled or not.</b> The cash left
///     the register on the day it was handed over. <c>SalaryEntry.NetPayable</c> already has the
///     advance <em>subtracted out of it</em>, so summing only paid entries would lose every
///     advance entirely — a pharmacy paying a fifth of its payroll as advances would report a
///     fifth less staff cost and that much more profit. Counting an advance when the salary that
///     nets it out is eventually paid would be no better: it would land in the wrong month, and an
///     advance never deducted would never land at all.</description>
///   </item>
/// </list>
///
/// <para>The two cannot double-count, and that is a property of the arithmetic rather than a rule
/// applied on top of it. An advance of 2,000 against a base of 15,000 contributes 2,000 here and a
/// <c>NetPayable</c> of 13,000 there: 15,000 in total, once, split across the months the money
/// actually moved in.</para>
///
/// <para><b>Two queries rather than one projection.</b> Each aggregates a plain column over one
/// base table and they are added in memory. Composing them as subqueries inside a single
/// projection reads better and fails at runtime — SQL Server rejects <em>"Cannot perform an
/// aggregate function on an expression containing an aggregate or a subquery"</em>, which this
/// codebase has hit in Modules 6, 7 and 8. See <see cref="SupplierBalanceQueries"/>.</para>
///
/// <para>No tenant id is passed. Both tables implement <c>ITenantEntity</c>, so the global query
/// filter scopes the sums to the pharmacy asking.</para>
/// </summary>
public sealed class OperatingExpenses : IOperatingExpenses
{
    private readonly ApplicationDbContext _context;

    public OperatingExpenses(ApplicationDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<decimal> GetOperatingExpensesAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        // Both ends inclusive, matching every other report in the system: a month runs from the
        // 1st to the last day, and an exclusive upper bound would silently drop the last day's
        // payroll.
        var salaries = await _context.SalaryEntries
            .AsNoTracking()
            .Where(e => e.PaymentStatus == SalaryPaymentStatus.Paid
                        && e.PaymentDate != null
                        && e.PaymentDate >= from
                        && e.PaymentDate <= to)
            .SumAsync(e => e.NetPayable, cancellationToken);

        var advances = await _context.SalaryAdvances
            .AsNoTracking()
            .Where(a => a.AdvanceDate >= from && a.AdvanceDate <= to)
            .SumAsync(a => a.Amount, cancellationToken);

        return salaries + advances;
    }
}
