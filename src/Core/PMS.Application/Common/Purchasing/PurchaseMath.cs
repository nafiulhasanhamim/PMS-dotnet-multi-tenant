using PMS.Application.Common.DTOs;

namespace PMS.Application.Common.Purchasing;

/// <summary>
/// The three derived figures this module shows everywhere, defined once.
///
/// <para>A purchase's due, its payment status and how much of a line can still go back each
/// appear on several screens and in two API shapes. Working any of them out at the point of use
/// is how the purchases list and the purchase detail page end up disagreeing about whether a bill
/// is settled.</para>
///
/// <para>The supplier-level balance is <em>not</em> here — it lives in
/// <c>ISupplierBalanceQueries</c> because it needs the database. These are pure arithmetic over
/// figures the caller already has.</para>
///
/// <para><b>General payments are folded into the due at display time.</b> Money paid against the
/// account rather than a bill still reduces what the supplier is owed, so a page that ignored it
/// per bill showed a settled account above a purchase marked Unpaid. The allocation is computed
/// per supplier, oldest bill first, and passed in — it is never written to the purchase. See the
/// <c>generalPaymentApplied</c> parameter.</para>
/// </summary>
public static class PurchaseMath
{
    /// <summary>
    /// What is still owed on one bill.
    ///
    /// <para><b>Returns are subtracted, payments are not re-derived.</b> Goods that went back are
    /// not goods that were bought, so they reduce the debt — but they do not reduce
    /// <c>AmountPaid</c>, because money already handed over has not come back. That asymmetry is
    /// the reason this can go negative.</para>
    /// </summary>
    /// <param name="generalPaymentApplied">
    /// Payments made against the <em>account</em> rather than this bill, notionally allocated to
    /// it — see <c>ISupplierBalanceQueries.GetGeneralPaymentAllocationsAsync</c>.
    ///
    /// <para><b>Presentational, and never stored.</b> Nothing writes this into
    /// <c>Purchase.AmountPaid</c>, because a guess written into the data cannot later be told
    /// apart from a real allocation. But leaving it out of the display produced a supplier page
    /// that said "nothing owed" above a bill marked "Unpaid, 765.00 due" — both figures correct,
    /// and together unreadable.</para>
    /// </param>
    /// <param name="creditSettled">
    /// A refund received, or a credit written off, clearing a credit this bill was holding. Adds
    /// back, because the credit is gone: the supplier returned the cash, or nobody is going to
    /// collect it. Also display-only.
    /// </param>
    public static decimal Due(
        decimal totalAmount,
        decimal amountPaid,
        decimal returnedAmount,
        decimal generalPaymentApplied = 0m,
        decimal creditSettled = 0m) =>
        totalAmount - amountPaid - returnedAmount - generalPaymentApplied + creditSettled;

    /// <summary>
    /// Where a bill stands.
    ///
    /// <para><b>Nothing owed means Paid, even when nothing was paid.</b> A delivery returned in
    /// full leaves a due of zero, and labelling that "Unpaid" would send somebody looking for
    /// money to hand over. The status answers "is there anything to settle", which is what the
    /// column is read for.</para>
    ///
    /// <para>An overpaid bill — due below zero — is also Paid. The negative figure is shown in
    /// the Due column, where it belongs; a fourth status for it would need handling on every
    /// screen to say something the number already says.</para>
    /// </summary>
    public static PurchasePaymentStatus StatusFor(
        decimal totalAmount,
        decimal amountPaid,
        decimal returnedAmount,
        decimal generalPaymentApplied = 0m,
        decimal creditSettled = 0m)
    {
        var due = Due(
            totalAmount, amountPaid, returnedAmount, generalPaymentApplied, creditSettled);

        if (due <= 0m)
        {
            return PurchasePaymentStatus.Paid;
        }

        // A general payment counts as money toward the bill for the purposes of this label, even
        // though it is not recorded against it. It IS money the supplier received, and a bill
        // three quarters covered by one reads wrongly as "Unpaid".
        return amountPaid + generalPaymentApplied > 0m
            ? PurchasePaymentStatus.PartiallyPaid
            : PurchasePaymentStatus.Unpaid;
    }

    /// <summary>
    /// How much of one purchase line can still be sent back.
    ///
    /// <para><b>Two caps, and the second is the one that gets forgotten.</b> The bill limits it —
    /// you cannot return more than was delivered, less what has already gone back. So does the
    /// shelf: if the stock was sold or written off, it is not there to hand over, whatever the
    /// invoice said. Taking the lower of the two is what stops a return driving a batch negative,
    /// and it is why a line can show zero returnable on a purchase that was never returned
    /// against at all.</para>
    ///
    /// <para>Floored at zero rather than allowed negative, which is reachable when a batch was
    /// adjusted down below what remained returnable on the bill.</para>
    /// </summary>
    public static int Returnable(
        int deliveredInBaseUnits, int alreadyReturnedInBaseUnits, int batchQuantityInBaseUnits)
    {
        var remainingOnBill = deliveredInBaseUnits - alreadyReturnedInBaseUnits;

        return Math.Max(0, Math.Min(remainingOnBill, batchQuantityInBaseUnits));
    }

    /// <summary>
    /// Whether physical stock, rather than the bill, is what limits a return.
    ///
    /// <para>The screen says which, because the two are different problems: "you already returned
    /// most of this" is a records question, and "it has been sold" is a stock one.</para>
    /// </summary>
    public static bool CappedByStock(
        int deliveredInBaseUnits, int alreadyReturnedInBaseUnits, int batchQuantityInBaseUnits) =>
        batchQuantityInBaseUnits < deliveredInBaseUnits - alreadyReturnedInBaseUnits;

    /// <summary>The prefix every purchase return puts on its stock adjustment's reason.</summary>
    public const string ReturnAdjustmentPrefix = "Purchase return: ";

    /// <summary>
    /// The reason text written onto the stock adjustment.
    ///
    /// <para>Tagged rather than copied verbatim so that the adjustment history reads as one event
    /// with the return — somebody looking at a batch's history sees why eighty pieces left, and
    /// that it was not a write-off.</para>
    /// </summary>
    public static string AdjustmentReason(string reason) => ReturnAdjustmentPrefix + reason.Trim();
}
