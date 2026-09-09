using System.Linq.Expressions;
using PMS.Domain.Entities;

namespace PMS.Application.Common.Stock;

/// <summary>
/// First Expire, First Out. The order in which a pharmacy must sell its stock, and the rule
/// every deduction in the system will follow.
///
/// <para><b>Why this is a helper and not a query.</b> Billing (Module 5) deducts from batches
/// in this order, the stock detail page shows them in this order so the sell-next batch is on
/// top, and expiry reporting reads the same order to decide what is at risk. Three
/// implementations of "earliest expiry first" would agree right up until one of them handled
/// null differently, and then a pharmacy would be selling the wrong pack while every screen
/// looked correct.</para>
///
/// <para><b>The null trap, twice over.</b> Some stock genuinely never expires — diapers,
/// syringes. Those batches must sell <em>after</em> anything with a date, because stock with a
/// deadline is the stock at risk. SQL Server sorts NULL <em>first</em> on an ascending order
/// by, which is the exact opposite, and LINQ-to-Objects does the same. So the ordering leads
/// with an explicit "has no expiry" key rather than relying on either provider's null
/// handling. Getting this wrong does not throw and does not look wrong on screen: it just
/// quietly sells the non-expiring stock first and lets the dated stock expire on the
/// shelf.</para>
///
/// <para><b>One definition, two providers.</b> The predicate and the three ordering keys are
/// declared once as expressions. The <see cref="IQueryable{T}"/> overloads hand them to EF for
/// translation to SQL; the <see cref="IEnumerable{T}"/> overloads use compiled copies of the
/// same expressions. There is no second place to edit, which is the point.</para>
/// </summary>
public static class Fefo
{
    // The three ordering keys. Order matters and each earns its place:
    //
    //   1. NullExpiryLast — false (0) sorts before true (1), so dated stock leads. This is
    //      the key that overrides both providers' NULLS FIRST default.
    //   2. ByExpiry       — the actual rule: soonest deadline first.
    //   3. ByCreated      — the tiebreaker. Two deliveries can share an expiry date, and
    //      without this the order between them is whatever the query plan happened to
    //      produce: stable enough to pass a test once and vary in production. Oldest
    //      delivery first, which is also what a pharmacist would do by hand.
    private static readonly Expression<Func<Batch, bool>> NullExpiryLast =
        batch => batch.ExpiryDate == null;

    private static readonly Expression<Func<Batch, DateOnly?>> ByExpiry =
        batch => batch.ExpiryDate;

    private static readonly Expression<Func<Batch, DateTime>> ByCreated =
        batch => batch.CreatedOnUtc;

    private static readonly Func<Batch, bool> NullExpiryLastFn = NullExpiryLast.Compile();
    private static readonly Func<Batch, DateOnly?> ByExpiryFn = ByExpiry.Compile();
    private static readonly Func<Batch, DateTime> ByCreatedFn = ByCreated.Compile();

    /// <summary>
    /// Whether a batch can be sold from, as at <paramref name="today"/>.
    ///
    /// <para>Three conditions, and the reason for each:</para>
    /// <list type="bullet">
    /// <item><description><c>IsActive</c> — a row entered in error is not stock. Note this is
    /// not depletion: a sold-out batch stays active and stays on the detail page, because it
    /// is the cost and expiry behind sales that already happened.</description></item>
    /// <item><description><c>QuantityInBaseUnits &gt; 0</c> — nothing to take.</description></item>
    /// <item><description>not expired — expired stock is still on the premises and still shown
    /// on screen in red, because somebody has to physically remove it. It is simply not
    /// sellable, and a deduction must never reach for it.</description></item>
    /// </list>
    /// </summary>
    public static Expression<Func<Batch, bool>> IsSellable(DateOnly today) =>
        batch => batch.IsActive
            && batch.QuantityInBaseUnits > 0
            && (batch.ExpiryDate == null || batch.ExpiryDate >= today);

    /// <summary>Sellable batches only, translated to SQL by EF.</summary>
    public static IQueryable<Batch> Sellable(this IQueryable<Batch> batches, DateOnly today)
        => batches.Where(IsSellable(today));

    /// <summary>Sellable batches only, in memory.</summary>
    public static IEnumerable<Batch> Sellable(this IEnumerable<Batch> batches, DateOnly today)
        => batches.Where(IsSellable(today).Compile());

    /// <summary>
    /// Orders batches for selling: soonest expiry first, non-expiring stock last, oldest
    /// delivery breaking a tie. Does not filter — compose with <see cref="Sellable(IQueryable{Batch}, DateOnly)"/>.
    /// </summary>
    public static IOrderedQueryable<Batch> InFefoOrder(this IQueryable<Batch> batches)
        => batches
            .OrderBy(NullExpiryLast)
            .ThenBy(ByExpiry)
            .ThenBy(ByCreated);

    /// <inheritdoc cref="InFefoOrder(IQueryable{Batch})"/>
    public static IOrderedEnumerable<Batch> InFefoOrder(this IEnumerable<Batch> batches)
        => batches
            .OrderBy(NullExpiryLastFn)
            .ThenBy(ByExpiryFn)
            .ThenBy(ByCreatedFn);

    /// <summary>
    /// <b>The helper.</b> Sellable batches for one product, in the order they must be sold.
    ///
    /// <para>Module 5 will walk this list taking what it needs from each batch in turn. It is
    /// built, tested and documented here, and deliberately not wired into any sale — there
    /// are no sales yet.</para>
    /// </summary>
    public static IEnumerable<Batch> GetActiveBatchesFefo(
        IEnumerable<Batch> allBatchesForProduct, DateOnly today)
        => allBatchesForProduct.Sellable(today).InFefoOrder();
}
