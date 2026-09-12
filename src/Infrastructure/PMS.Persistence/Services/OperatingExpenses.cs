using PMS.Application.Interfaces;

namespace PMS.Persistence.Services;

/// <summary>
/// The placeholder implementation of <see cref="IOperatingExpenses"/>.
///
/// <para><b>Returns zero because Module 9 (Salary) does not exist yet</b> — there is no salary
/// payment or advance to sum. It is registered and called for real, so the monthly report already
/// goes through the seam, and Module 9 completes the report by replacing this class rather than by
/// editing any report.</para>
///
/// <para><b>Deliberately not silent about it.</b> The monthly report renders a note beside the net
/// profit figure saying salaries are not yet included, because an owner reading "net profit" is
/// entitled to assume they are. A zero that looks like a real zero is the failure mode worth
/// avoiding here — it would understate cost and overstate profit by the largest recurring expense
/// a pharmacy has.</para>
/// </summary>
public sealed class OperatingExpenses : IOperatingExpenses
{
    /// <inheritdoc />
    public Task<decimal> GetOperatingExpensesAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        Task.FromResult(0m);
}
