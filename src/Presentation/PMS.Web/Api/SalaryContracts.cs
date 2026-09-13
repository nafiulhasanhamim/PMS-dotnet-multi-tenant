namespace PMS.Web.Api;

// ═══════════════════════════════════════════════════════════════════════════════════════════
// Module 9 wire contracts: salary profiles, advances and generated salary entries.
//
// Enum values must match the server's NUMERIC values — the API serialises enums as integers.
//
// Nothing here recomputes money that arrived computed. Net payable is decided once, at
// generation, by the domain; the only arithmetic below is presentation - adding two deductions
// that are already on the wire so a column can show one number.
//
// The exception is the generation screen, which genuinely computes a PREVIEW of net payable as
// somebody types. That figure is never submitted: the server recomputes it from the same rules
// and the response says what actually happened. See salary-generate.js.
// ═══════════════════════════════════════════════════════════════════════════════════════════

public enum SalaryProfileStatusFilter
{
    Active = 0,
    Inactive = 1,
    All = 2,
}

public enum AdvanceSettlementFilter
{
    All = 0,
    Unsettled = 1,
    Settled = 2,
}

public enum SalaryStatusFilter
{
    All = 0,
    Unpaid = 1,
    Paid = 2,
}

public enum SalaryPaymentStatus
{
    Unpaid = 0,
    Paid = 1,
}

// ── Profiles ─────────────────────────────────────────────────────────────────────────────

public sealed record SalaryProfileListItem(
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

public sealed record SalaryProfileDetail(
    Guid Id,
    Guid UserId,
    string EmployeeName,
    string EmployeeEmail,
    string? Designation,
    decimal MonthlyBaseSalary,
    DateOnly JoiningDate,
    bool IsActive);

/// <summary>
/// One option in the "add profile" dropdown: a user of this pharmacy who is not already on the
/// payroll. The server decides who qualifies; this list is exactly what it will accept.
/// </summary>
public sealed record SalaryEligibleUser(Guid UserId, string Name, string Email, string Role);

public sealed record SalaryProfileOption(
    Guid ProfileId,
    string EmployeeName,
    string? Designation,
    decimal MonthlyBaseSalary,
    decimal UnsettledAdvanceTotal);

// ── Advances ─────────────────────────────────────────────────────────────────────────────

public sealed record SalaryAdvanceRow(
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

public sealed record UnsettledAdvance(
    Guid Id,
    decimal Amount,
    DateOnly AdvanceDate,
    string? Reason);

public sealed record SalaryAdvanceRecorded(
    Guid Id,
    Guid ProfileId,
    string EmployeeName,
    decimal Amount,
    DateOnly AdvanceDate,
    decimal UnsettledTotalAfter);

// ── Generation ───────────────────────────────────────────────────────────────────────────

/// <param name="AlreadyGenerated">
/// Rendered greyed and unselectable rather than omitted. An employee missing from the list is
/// indistinguishable from one nobody put on the payroll.
/// </param>
public sealed record SalaryGenerationRow(
    Guid ProfileId,
    Guid UserId,
    string EmployeeName,
    string? Designation,
    decimal BaseSalary,
    decimal UnsettledAdvanceTotal,
    IReadOnlyList<UnsettledAdvance> UnsettledAdvances,
    bool AlreadyGenerated,
    Guid? ExistingEntryId)
{
    /// <summary>
    /// What this month cannot recover, before any bonus or other deduction is typed in — so the
    /// warning can render on first paint rather than only after a keystroke.
    /// </summary>
    public decimal Overshoot => Math.Max(0m, UnsettledAdvanceTotal - BaseSalary);

    /// <summary>Net payable as the row first loads: base less whatever the advances take.</summary>
    public decimal InitialNetPayable =>
        Math.Max(0m, BaseSalary - Math.Min(UnsettledAdvanceTotal, BaseSalary));
}

public sealed record SalaryGenerationPreview(
    int Month,
    int Year,
    IReadOnlyList<SalaryGenerationRow> Rows)
{
    public static SalaryGenerationPreview Empty { get; } = new(0, 0, Array.Empty<SalaryGenerationRow>());

    public IEnumerable<SalaryGenerationRow> Eligible => Rows.Where(r => !r.AlreadyGenerated);

    public int EligibleCount => Rows.Count(r => !r.AlreadyGenerated);

    public int AlreadyGeneratedCount => Rows.Count(r => r.AlreadyGenerated);
}

public sealed record SalaryGenerationLine(
    Guid ProfileId,
    decimal Bonus,
    decimal AdvanceDeduction,
    decimal OtherDeduction,
    string? AdjustmentNotes);

public sealed record SalaryGenerationResult(
    int Month,
    int Year,
    int EntriesCreated,
    decimal TotalNetPayable,
    decimal AdvancesSettled,
    decimal AdvancesCarriedOver);

// ── Entries ──────────────────────────────────────────────────────────────────────────────

public sealed record SalaryEntryRow(
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
    /// Whether the Edit action is rendered at all. Sent by the server rather than inferred, so
    /// the screen and the API cannot disagree about what is locked.
    /// </summary>
    public bool CanEdit => PaymentStatus != SalaryPaymentStatus.Paid;
}

public sealed record SalarySlip(
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
    IReadOnlyList<UnsettledAdvance> SettledAdvances,
    string GeneratedByName,
    DateTime GeneratedOnUtc)
{
    public decimal TotalDeductions => AdvanceDeduction + OtherDeduction;
}

// ── Landing ──────────────────────────────────────────────────────────────────────────────

public sealed record SalarySummary(
    int UnpaidEntryCount,
    decimal UnpaidEntryTotal,
    int UnsettledAdvanceCount,
    decimal UnsettledAdvanceTotal,
    int ActiveProfileCount)
{
    public static SalarySummary Empty { get; } = new(0, 0m, 0, 0m, 0);
}
