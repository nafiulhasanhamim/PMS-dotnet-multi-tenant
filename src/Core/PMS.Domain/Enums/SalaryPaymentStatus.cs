namespace PMS.Domain.Enums;

/// <summary>
/// Whether a generated salary has actually been handed over.
///
/// <para><b>Two states, not three.</b> There is no "Partially paid": a half-paid salary is a
/// conversation between an owner and an employee, not a record the pharmacy needs to reconcile,
/// and the honest way to record one is a smaller salary now and a bonus next month. Adding the
/// state would put every screen and every expense total into the business of apportioning it.</para>
///
/// <para>Named <c>Salary</c>PaymentStatus rather than <c>PaymentStatus</c> because supplier
/// payments and sales both have a notion of payment status already, and a bare name here would
/// eventually be imported into the wrong file.</para>
/// </summary>
public enum SalaryPaymentStatus
{
    /// <summary>
    /// Generated but not yet handed over. Editable: bonus, deductions and notes can all still be
    /// corrected, and the entry can be deleted outright.
    /// </summary>
    Unpaid = 0,

    /// <summary>
    /// Paid, on <c>SalaryEntry.PaymentDate</c>.
    ///
    /// <para><b>Immutable from here</b>, and counted as an operating expense on the payment date
    /// rather than in the month it covers. See <c>SalaryEntry</c>.</para>
    /// </summary>
    Paid = 1,
}
