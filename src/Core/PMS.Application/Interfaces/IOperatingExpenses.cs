namespace PMS.Application.Interfaces;

/// <summary>
/// What it cost to keep the doors open in a period — the figure that turns gross profit into net.
///
/// <para>Module 8 introduced this seam and shipped an implementation returning zero, with the
/// monthly report saying so on screen. Module 9 replaced that one class. No report changed, no
/// report forgot to include the new figure, and nothing had to be hunted for — which is the whole
/// argument for having declared the interface a module early.</para>
///
/// <para><b>Staff cost is the only thing in here today</b>, because it is the only recurring
/// expense the system records. Rent, utilities and the rest would be a fourth table and a fourth
/// screen; when one arrives it is added to the implementation and every report picks it up
/// unchanged, exactly as salaries did.</para>
/// </summary>
public interface IOperatingExpenses
{
    /// <summary>
    /// Operating expenses that occurred between <paramref name="from"/> and
    /// <paramref name="to"/>, both inclusive.
    ///
    /// <para><b>By the date the money moved, not the month it relates to.</b> A salary counts on
    /// the day it was paid — an August salary paid on 2 September is a September expense — and an
    /// advance counts on the day the cash was handed over. This matches how Module 8 attributes a
    /// return to the day the goods came back rather than the day of the original sale, and it is
    /// what makes the figure reconcile against a till.</para>
    ///
    /// <para>See <c>OperatingExpenses</c> for why both salary entries and advances have to be
    /// summed, and why neither double-counts the other.</para>
    /// </summary>
    Task<decimal> GetOperatingExpensesAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
