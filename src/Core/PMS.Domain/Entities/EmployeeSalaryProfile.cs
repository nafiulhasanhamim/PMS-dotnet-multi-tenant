using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;

namespace PMS.Domain.Entities;

/// <summary>
/// What one member of staff is paid, and on what terms.
///
/// <para><b>A separate table from <c>User</c>, deliberately.</b> Not every user draws a salary —
/// the owner running the system as Admin frequently does not — so these would be nullable columns
/// on most rows. And a user's identity is read on every single request; their designation and what
/// they earn are read by one Admin a few times a month. Keeping HR and financial data out of the
/// authentication table means a bug in one cannot expose the other.</para>
///
/// <para><b>Deactivation is a soft delete and must stay one.</b> Every salary entry and every
/// advance ever recorded points at this row. Deleting it would either orphan a year of payslips or
/// cascade them away, and those are the record of money that left the till. Someone who leaves is
/// deactivated: they disappear from the generation screen and from the advance dropdown, and every
/// month they were paid for stays exactly where it was.</para>
/// </summary>
public sealed class EmployeeSalaryProfile : BaseAuditableAggregateRoot<Guid>, ITenantEntity
{
    // EF materialises through this.
    private EmployeeSalaryProfile()
    {
    }

    public EmployeeSalaryProfile(
        Guid userId,
        string? designation,
        decimal monthlyBaseSalary,
        DateOnly joiningDate)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Designation = Blank(designation);
        MonthlyBaseSalary = monthlyBaseSalary;
        JoiningDate = joiningDate;
        IsActive = true;
    }

    /// <summary>Stamped by the persistence interceptor on insert; never set by a handler.</summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Who this is. One active profile per user per pharmacy, enforced by a filtered unique index
    /// — a person cannot be on the payroll twice, but a rejoiner can have a second profile once
    /// the first is deactivated.
    /// </summary>
    public Guid UserId { get; private set; }

    public User User { get; private set; } = null!;

    /// <summary>"Counter staff", "Pharmacist". Free text: a fixed list would not survive a year.</summary>
    public string? Designation { get; private set; }

    /// <summary>
    /// What they earn a month, <em>now</em>.
    ///
    /// <para><b>Changing this never rewrites a generated entry.</b> Each <c>SalaryEntry</c> copies
    /// the figure at generation time precisely so that a raise in October does not restate what
    /// somebody was paid in July — see that entity.</para>
    /// </summary>
    public decimal MonthlyBaseSalary { get; private set; }

    public DateOnly JoiningDate { get; private set; }

    /// <summary>Whether they are still on the payroll. See the class remarks: never deleted.</summary>
    public bool IsActive { get; private set; }

    public void Update(string? designation, decimal monthlyBaseSalary, DateOnly joiningDate)
    {
        Designation = Blank(designation);
        MonthlyBaseSalary = monthlyBaseSalary;
        JoiningDate = joiningDate;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
