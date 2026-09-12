namespace PMS.Application.Interfaces;

/// <summary>
/// What a supplier is owed, and the three figures it comes from.
///
/// <para><b><see cref="Outstanding"/> is the formula, and this is the only place it is
/// written.</b> The supplier list, the supplier detail page, the purchases list and Module 8's
/// dues report all read it from here. Two implementations of this subtraction would eventually
/// disagree, and at that point neither number can be trusted — which for a figure a pharmacy
/// pays money against is the worst failure this module has available.</para>
/// </summary>
/// <param name="TotalPurchased">Sum of the supplier's purchase totals.</param>
/// <param name="TotalReturned">Sum of returns against this supplier's purchase lines.</param>
/// <param name="TotalPaid">
/// Money out, purchase-linked and general alike. <b>Payments only</b> — refunds and write-offs
/// are counted separately, because a sum that mixed the two directions would be meaningless.
/// </param>
/// <param name="TotalRefunded">
/// Money the supplier handed back, settling a credit. Moves the balance <em>up</em>, toward what
/// the pharmacy owes.
/// </param>
/// <param name="TotalWrittenOff">
/// Credit given up, with no cash moving — a distributor that closed, or an amount too small to
/// chase. Moves the balance exactly as a refund does, and is kept apart from one because only
/// one of them shows up in a till reconciliation.
/// </param>
public sealed record SupplierBalance(
    decimal TotalPurchased,
    decimal TotalReturned,
    decimal TotalPaid,
    decimal TotalRefunded = 0m,
    decimal TotalWrittenOff = 0m)
{
    public static SupplierBalance Zero { get; } = new(0m, 0m, 0m, 0m, 0m);

    /// <summary>
    /// What is still owed.
    ///
    /// <para><b>Can legitimately be negative</b>, and nothing clamps it. A pharmacy that paid a
    /// bill in full and then returned half the delivery is owed money by its supplier, and a
    /// figure floored at zero would hide a real credit. Every screen that shows a balance is
    /// written to say so rather than to pretend it is settled.</para>
    ///
    /// <para>Refunds and write-offs add back because they undo a credit: the supplier returning
    /// the cash, or the pharmacy giving up on collecting it, both leave nothing owed either
    /// way.</para>
    /// </summary>
    public decimal Outstanding =>
        TotalPurchased - TotalReturned - TotalPaid + TotalRefunded + TotalWrittenOff;

    public bool IsOwing => Outstanding > 0m;

    /// <summary>
    /// The supplier owes the pharmacy.
    ///
    /// <para>Named for the balance rather than for its cause: a credit arises from an overpayment
    /// <em>or</em> from goods returned after a bill was paid, and the screens say "in credit"
    /// because only one of those is an overpayment.</para>
    /// </summary>
    public bool IsOverpaid => Outstanding < 0m;

    public bool IsSettled => Outstanding == 0m;

    /// <summary>What the supplier would have to hand back to settle a credit.</summary>
    public decimal CreditAvailable => IsOverpaid ? -Outstanding : 0m;
}

/// <summary>
/// The outstanding-balance calculation, in one place.
///
/// <para><b>Never stored as a column.</b> The balance is derived from three tables that move
/// independently — a purchase is recorded, a payment goes out, goods go back — and a stored total
/// is wrong from the moment any one of them changes without it. Recomputing on read costs three
/// indexed aggregates and cannot drift.</para>
///
/// <para><b>Shaped to avoid a translation failure this codebase has hit three times.</b> SQL
/// Server refuses to aggregate over an expression that itself contains an aggregate or a
/// subquery, so none of these methods computes a balance inside a projection over suppliers.
/// Each instead runs the three sums as their own grouped queries over the base tables and
/// combines them in memory. That is why <see cref="GetBalancesAsync"/> takes a set of ids: the
/// caller pages the suppliers first, then asks for balances for just that page — and the export
/// path asks the same method for every id, so the two cannot produce different SQL.</para>
/// </summary>
public interface ISupplierBalanceQueries
{
    /// <summary>One supplier's balance. Zero for a supplier with no activity at all.</summary>
    Task<SupplierBalance> GetBalanceAsync(
        Guid supplierId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Balances for a known set of suppliers, keyed by id.
    ///
    /// <para>Suppliers with no purchases, payments or returns are present with
    /// <see cref="SupplierBalance.Zero"/> rather than absent, so a caller never has to decide
    /// what a missing key means.</para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, SupplierBalance>> GetBalancesAsync(
        IReadOnlyCollection<Guid> supplierIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activity for every supplier in the pharmacy, optionally narrowed to a period.
    ///
    /// <para><b>A date range narrows the three activity figures, never the meaning of
    /// "outstanding".</b> What a supplier is owed is a fact about now, not about a window — a
    /// balance computed from one month's purchases and one month's payments is not money anybody
    /// owes anybody. Callers that show both must label them as what they are; see the dues
    /// report.</para>
    ///
    /// <para>Purchases are filtered by <c>PurchaseDate</c> and payments by <c>PaymentDate</c> —
    /// the dates on the documents. Returns are filtered by when the return happened, matching how
    /// Module 8 attributes sales returns.</para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, SupplierBalance>> GetActivityAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);

    /// <summary>
    /// How much of each supplier's <b>general</b> payments to show against each of their bills,
    /// keyed by purchase id.
    ///
    /// <para><b>Display only. Nothing here is ever written.</b> A payment recorded against the
    /// account rather than a bill genuinely does not say which bill it settled, and storing a
    /// guess would make it indistinguishable from a real allocation forever. But omitting it from
    /// the screen was worse: a supplier page showed "Outstanding 0.00 — nothing owed" directly
    /// above a purchase badged "Unpaid, 765.00 due". Both figures were right and the pair was
    /// unreadable.</para>
    ///
    /// <para><b>Oldest bill first</b>, which is how a supplier applies an unallocated payment and
    /// what the module brief specifies. Bills already settled — or in credit from a return — are
    /// skipped rather than absorbing any, and no bill takes more than it owes. Whatever is left
    /// over is simply not allocated, which is the honest outcome for a supplier who has been
    /// overpaid.</para>
    ///
    /// <para>The consequence worth relying on: for each supplier, the sum of their bills' dues
    /// after allocation equals their outstanding balance. That is what makes the two figures on
    /// the supplier page reconcile.</para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, BillAdjustment>> GetGeneralPaymentAllocationsAsync(
        IReadOnlyCollection<Guid> supplierIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// What account-level money does to one bill, for display.
///
/// <para>Two figures moving in opposite directions, and both are views rather than records —
/// nothing is written to the purchase for either.</para>
/// </summary>
/// <param name="GeneralPaymentApplied">
/// Payments made against the account rather than this bill, covering what it owes. Reduces its
/// due.
/// </param>
/// <param name="CreditSettled">
/// A refund received, or a credit written off, clearing a credit this bill was holding. Raises its
/// due back toward zero — the mirror image of the field above, and what keeps a supplier's bills
/// adding up to their balance once money comes back the other way.
/// </param>
public sealed record BillAdjustment(decimal GeneralPaymentApplied, decimal CreditSettled)
{
    public static BillAdjustment None { get; } = new(0m, 0m);
}
