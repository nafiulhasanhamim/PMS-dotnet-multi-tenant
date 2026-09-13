namespace PMS.Domain.Enums;

/// <summary>
/// Which way money moved between the pharmacy and a supplier.
///
/// <para><b>A direction rather than a negative amount.</b> The alternative — allowing
/// <c>SupplierPayment.Amount</c> below zero — would leave a table called <em>payments</em> holding
/// receipts, and every sum over it would need a sign convention nobody could see from the column
/// name. The amount stays positive and this says what it was.</para>
///
/// <para>All three affect the balance; only two of them involve money actually moving.</para>
/// </summary>
public enum SupplierPaymentDirection
{
    /// <summary>
    /// Money out, to the supplier. The default, and everything recorded before this existed.
    /// Reduces what they are owed.
    /// </summary>
    Payment = 0,

    /// <summary>
    /// Money back, from the supplier, settling a credit.
    ///
    /// <para>A credit arises when goods go back after the bill was paid, or when a payment
    /// overshot. The supplier can clear it either by crediting the next delivery — which needs
    /// nothing recorded, because the next purchase absorbs it — or by handing the cash back, which
    /// is this. <b>Increases</b> what they are owed, back toward zero.</para>
    /// </summary>
    Refund = 1,

    /// <summary>
    /// A credit given up. No money moved.
    ///
    /// <para>For a credit that will never be collected: the distributor has closed, the amount is
    /// too small to chase, or nobody is going to buy from them again. Without it such a credit
    /// sits on the dues report forever, and a figure nobody intends to act on teaches people to
    /// ignore the column it is in.</para>
    ///
    /// <para>Moves the balance exactly as a refund does, and is kept distinct from one precisely
    /// because no cash arrived — a pharmacy reconciling its till must be able to tell them
    /// apart.</para>
    /// </summary>
    WriteOff = 2,
}
