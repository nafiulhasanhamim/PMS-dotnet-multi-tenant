namespace PMS.Application.Interfaces;

/// <summary>
/// What it cost to keep the doors open in a period — the figure that turns gross profit into net.
///
/// <para><b>This returns zero today, and that is a placeholder rather than a business rule.</b>
/// The Salary module is Module 9 and does not exist. Every report that needs operating expenses
/// calls this now, so completing Module 9 is a matter of filling in one method body — no report
/// code changes, no report forgets to include the new figure, and nothing has to be hunted
/// for.</para>
///
/// <para>The monthly report says so on screen while it returns zero, rather than presenting gross
/// profit under a heading that says net. An owner reading "net profit" is entitled to assume
/// salaries are in it.</para>
/// </summary>
public interface IOperatingExpenses
{
    /// <summary>
    /// Operating expenses that occurred between <paramref name="from"/> and
    /// <paramref name="to"/>, both inclusive.
    ///
    /// <para><b>TODO: implement in Module 9 (Salary).</b> Must return the sum of salary payments
    /// plus salary advances that occurred in this period — by the date the money moved, not the
    /// month the salary was for, matching how Module 8 attributes returns to the period they
    /// happened in. See the Module 9 documentation.</para>
    /// </summary>
    Task<decimal> GetOperatingExpensesAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
