using PMS.Domain.Enums;

namespace PMS.Application.Common.DTOs;

// ── Filters ──────────────────────────────────────────────────────────────────────────────

/// <summary>Whether a profile list shows active rows, inactive rows, or both.</summary>
public enum SalaryProfileStatusFilter
{
    Active = 0,
    Inactive = 1,
    All = 2,
}

/// <summary>Whether an advances list shows what is still owed, what has been recovered, or both.</summary>
public enum AdvanceSettlementFilter
{
    All = 0,
    Unsettled = 1,
    Settled = 2,
}

/// <summary>Whether a salary history shows unpaid entries, paid ones, or both.</summary>
public enum SalaryStatusFilter
{
    All = 0,
    Unpaid = 1,
    Paid = 2,
}

// ── Profiles ─────────────────────────────────────────────────────────────────────────────

/// <param name="UnsettledAdvanceTotal">
/// What this employee still owes back. Counted in a second round-trip rather than projected per
/// row — see <c>ISalaryQueries</c> for why that shape is forced rather than chosen.
/// </param>
public sealed record SalaryProfileListItemDto(
    Guid Id,
    Guid UserId,
    string EmployeeName,
    string EmployeeEmail,
    string? Designation,
    decimal MonthlyBaseSalary,
    DateOnly JoiningDate,
    bool IsActive,
    decimal UnsettledAdvanceTotal,
    int UnsettledAdvanceCount);

public sealed record SalaryProfileDetailDto(
    Guid Id,
    Guid UserId,
    string EmployeeName,
    string EmployeeEmail,
    string? Designation,
    decimal MonthlyBaseSalary,
    DateOnly JoiningDate,
    bool IsActive);

/// <summary>
/// One entry in the "add profile" dropdown.
///
/// <para>Only users without an active profile appear. Offering somebody already on the payroll
/// would offer a row the filtered unique index refuses, and a dropdown whose options can fail is
/// worse than a shorter dropdown.</para>
/// </summary>
public sealed record SalaryEligibleUserDto(Guid UserId, string Name, string Email, string Role);

/// <summary>Active profiles, for the advance form's employee picker.</summary>
public sealed record SalaryProfileOptionDto(
    Guid ProfileId,
    string EmployeeName,
    string? Designation,
    decimal MonthlyBaseSalary,
    decimal UnsettledAdvanceTotal);

// ── Advances ─────────────────────────────────────────────────────────────────────────────

/// <param name="SettledInMonth">
/// Which month's salary recovered it, formatted, or null while it is still outstanding. An
/// employee asking where their money went deserves an answer that names the month.
/// </param>
public sealed record SalaryAdvanceRowDto(
    Guid Id,
    Guid ProfileId,
    string EmployeeName,
    string? Designation,
    decimal Amount,
    DateOnly AdvanceDate,
    string? Reason,
    string GivenByName,
    bool IsSettled,
    Guid? SettledInSalaryEntryId,
    int? SettledInMonth,
    int? SettledInYear);

/// <summary>One unsettled advance, as the generation screen's expandable breakdown shows it.</summary>
public sealed record UnsettledAdvanceDto(
    Guid Id,
    decimal Amount,
    DateOnly AdvanceDate,
    string? Reason);

/// <summary>What recording one advance produced, plus what the employee now owes in total.</summary>
public sealed record SalaryAdvanceRecordedDto(
    Guid Id,
    Guid ProfileId,
    string EmployeeName,
    decimal Amount,
    DateOnly AdvanceDate,
    decimal UnsettledTotalAfter);

// ── Generation ───────────────────────────────────────────────────────────────────────────

/// <summary>
/// One row of the generation screen.
///
/// <para><b>Profiles already generated are returned flagged, not omitted.</b> An employee missing
/// from the list is indistinguishable from an employee the pharmacy forgot to put on the payroll;
/// a greyed row labelled "Already generated" answers the question the screen is actually being
/// read to answer.</para>
/// </summary>
/// <param name="UnsettledAdvances">
/// Every unsettled advance for this profile whatever its date — an advance from two months ago
/// that was never recovered still belongs here. This list is what replaces relying on somebody's
/// memory at month-end.
/// </param>
public sealed record SalaryGenerationRowDto(
    Guid ProfileId,
    Guid UserId,
    string EmployeeName,
    string? Designation,
    decimal BaseSalary,
    decimal UnsettledAdvanceTotal,
    IReadOnlyList<UnsettledAdvanceDto> UnsettledAdvances,
    bool AlreadyGenerated,
    Guid? ExistingEntryId)
{
    /// <summary>
    /// What the month can actually recover, before any bonus or other deduction is typed in.
    /// Shown so the overshoot warning can be rendered before a single keystroke.
    /// </summary>
    public decimal Overshoot => Math.Max(0m, UnsettledAdvanceTotal - BaseSalary);
}

public sealed record SalaryGenerationPreviewDto(
    int Month,
    int Year,
    IReadOnlyList<SalaryGenerationRowDto> Rows)
{
    public int EligibleCount => Rows.Count(r => !r.AlreadyGenerated);

    public int AlreadyGeneratedCount => Rows.Count(r => r.AlreadyGenerated);
}

/// <summary>
/// One employee's figures on a single press of Generate.
///
/// <para><c>AdvanceDeduction</c> is a <em>request</em>, not a result. What is actually recovered
/// is capped by what the month can bear and by which advances fit whole — see
/// <c>SalaryEntry.Generate</c>. The entry that comes back says what really happened.</para>
/// </summary>
public sealed record SalaryGenerationLine(
    Guid ProfileId,
    decimal Bonus,
    decimal AdvanceDeduction,
    decimal OtherDeduction,
    string? AdjustmentNotes);

/// <summary>What one press of Generate produced.</summary>
public sealed record SalaryGenerationResultDto(
    int Month,
    int Year,
    int EntriesCreated,
    decimal TotalNetPayable,
    decimal AdvancesSettled,
    decimal AdvancesCarriedOver);

// ── Entries ──────────────────────────────────────────────────────────────────────────────

public sealed record SalaryEntryRowDto(
    Guid Id,
    Guid ProfileId,
    string EmployeeName,
    string? Designation,
    int Month,
    int Year,
    decimal BaseSalary,
    decimal Bonus,
    decimal AdvanceDeduction,
    decimal OtherDeduction,
    string? AdjustmentNotes,
    decimal NetPayable,
    SalaryPaymentStatus PaymentStatus,
    DateOnly? PaymentDate)
{
    public decimal TotalDeductions => AdvanceDeduction + OtherDeduction;

    /// <summary>
    /// Whether the edit action should exist at all. A paid entry is immutable — the money has
    /// gone and the employee has a slip.
    /// </summary>
    public bool CanEdit => PaymentStatus != SalaryPaymentStatus.Paid;
}

/// <summary>
/// Everything the printable slip needs.
///
/// <para>Pharmacy identity is a placeholder until Module 10's settings, exactly as the invoice's
/// is.</para>
/// </summary>
public sealed record SalarySlipDto(
    Guid Id,
    string EmployeeName,
    string EmployeeEmail,
    string? Designation,
    DateOnly JoiningDate,
    int Month,
    int Year,
    decimal BaseSalary,
    decimal Bonus,
    decimal AdvanceDeduction,
    decimal OtherDeduction,
    string? AdjustmentNotes,
    decimal NetPayable,
    SalaryPaymentStatus PaymentStatus,
    DateOnly? PaymentDate,
    IReadOnlyList<UnsettledAdvanceDto> SettledAdvances,
    string GeneratedByName,
    DateTime GeneratedOnUtc)
{
    public decimal TotalDeductions => AdvanceDeduction + OtherDeduction;
}

// ── Landing summary ──────────────────────────────────────────────────────────────────────

/// <summary>
/// The two figures on the salary hub.
///
/// <para>Both are counts-and-totals over one base table each, deliberately: composing them into a
/// single projection is the shape SQL Server has rejected three times in this codebase. See
/// <c>ISalaryQueries</c>.</para>
/// </summary>
public sealed record SalarySummaryDto(
    int UnpaidEntryCount,
    decimal UnpaidEntryTotal,
    int UnsettledAdvanceCount,
    decimal UnsettledAdvanceTotal,
    int ActiveProfileCount);
