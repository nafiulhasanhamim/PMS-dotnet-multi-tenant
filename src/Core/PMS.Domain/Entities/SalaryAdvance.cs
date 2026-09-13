using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;

namespace PMS.Domain.Entities;

/// <summary>
/// Cash handed to an employee mid-month, to come out of their next salary.
///
/// <para><b>Recorded when it is given, not remembered at month-end.</b> That is the whole reason
/// this table exists. An advance typed in as a number during salary generation depends on somebody
/// recalling a ৳2,000 note handed over three weeks earlier — and when they do not, the employee is
/// paid in full on top of money they have already had.</para>
///
/// <para><b>It is an expense on the day it was given.</b> The cash left the register then. Counting
/// it when the salary that nets it out is eventually generated would put it in the wrong month, and
/// summing only <c>SalaryEntry.NetPayable</c> would lose it entirely — that figure already has the
/// advance subtracted out of it. See <c>IOperatingExpenses</c> and the module doc's worked
/// example.</para>
///
/// <para><b>Unsettled advances have no expiry.</b> Generation looks for every unsettled row
/// regardless of its date, so one given in June and never deducted still appears in October. An
/// advance the system quietly forgot would be worse than no advance tracking at all.</para>
///
/// <para><b>A row is a tranche, and a tranche is settled whole or not at all.</b> Usually a row is
/// exactly one handover. When a month can only recover part of one — ৳15,000 of a ৳16,000 advance
/// against a ৳15,000 salary — the row <see cref="SplitOff"/>s: it shrinks to the ৳1,000 remainder
/// and a settled ৳15,000 tranche is created beside it, carrying the same date, reason and giver.
/// The alternative, a part-settled row, would need a column recording how much each of several
/// salary entries had recovered from it, which is a join table wearing a disguise. This way
/// <see cref="IsSettled"/> stays a fact rather than a comparison, the outstanding total is a plain
/// sum, and the expense figure is untouched: the two tranches carry the same
/// <see cref="AdvanceDate"/> and still total what was handed over.</para>
/// </summary>
public sealed class SalaryAdvance : BaseAuditableAggregateRoot<Guid>, ITenantEntity
{
    // EF materialises through this.
    private SalaryAdvance()
    {
    }

    public SalaryAdvance(
        Guid employeeSalaryProfileId,
        decimal amount,
        DateOnly advanceDate,
        string? reason,
        Guid givenByUserId)
    {
        Id = Guid.NewGuid();
        EmployeeSalaryProfileId = employeeSalaryProfileId;
        Amount = amount;
        AdvanceDate = advanceDate;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        GivenByUserId = givenByUserId;
        IsSettled = false;
    }

    /// <summary>Stamped by the persistence interceptor on insert; never set by a handler.</summary>
    public Guid TenantId { get; set; }

    public Guid EmployeeSalaryProfileId { get; private set; }

    public EmployeeSalaryProfile EmployeeSalaryProfile { get; private set; } = null!;

    /// <summary>
    /// Must be greater than zero. Money going the other way is a salary, not an advance.
    ///
    /// <para>Falls when this row is split — see <see cref="SplitOff"/> and the class remarks.
    /// Nothing else ever changes it.</para>
    /// </summary>
    public decimal Amount { get; private set; }

    /// <summary>
    /// The day the cash was handed over. <b>This is the date the expense falls on</b>, not the
    /// date it is later deducted from a salary.
    /// </summary>
    public DateOnly AdvanceDate { get; private set; }

    /// <summary>"Medical emergency". Optional, and worth asking for: it is what makes the
    /// breakdown on the generation screen mean something months later.</summary>
    public string? Reason { get; private set; }

    /// <summary>Who handed it over. Admin-only, so this is always an owner.</summary>
    public Guid GivenByUserId { get; private set; }

    /// <summary>Whether a salary entry has deducted it. Unsettled rows carry forward indefinitely.</summary>
    public bool IsSettled { get; private set; }

    /// <summary>
    /// Which salary entry settled it, once one has.
    ///
    /// <para>Kept so the advances list can say "settled in August 2026" rather than merely
    /// "settled" — an employee asking where their money went deserves an answer that names the
    /// month.</para>
    /// </summary>
    public Guid? SettledInSalaryEntryId { get; private set; }

    public SalaryEntry? SettledInSalaryEntry { get; private set; }

    /// <summary>
    /// Marks this advance as deducted by a salary entry.
    ///
    /// <para>Internal: only <see cref="SalaryEntry"/>'s settlement path calls it, immediately
    /// after deciding which advances this month's deduction actually covers. Settlement without a
    /// deduction to back it would leave money nobody ever recovers.</para>
    /// </summary>
    internal void SettleIn(Guid salaryEntryId)
    {
        IsSettled = true;
        SettledInSalaryEntryId = salaryEntryId;
    }

    /// <summary>
    /// Puts this advance back in the queue, unsettled.
    ///
    /// <para>Internal, and called from one place: <see cref="SalaryEntry.Revise"/>, which releases
    /// everything it had settled before re-running settlement. Editing an unpaid entry's advance
    /// deduction downwards must genuinely free the advances back up — otherwise rows stay marked
    /// settled against money the pharmacy is no longer recovering, and it recovers them
    /// twice.</para>
    ///
    /// <para>A tranche released this way is <em>not</em> merged back into the row it was split
    /// from. Two unsettled rows of ৳15,000 and ৳1,000 where one ৳16,000 handover used to be is
    /// untidy but never wrong: the outstanding total, the dates and the expense figure are all
    /// unchanged, and re-running settlement takes them oldest-first exactly as before.</para>
    ///
    /// <para>There is no public "unsettle" for the same reason there is no editing a paid entry:
    /// an advance's settlement is a consequence of some month's arithmetic, never a thing set on
    /// its own.</para>
    /// </summary>
    internal void Release()
    {
        IsSettled = false;
        SettledInSalaryEntryId = null;
    }

    /// <summary>
    /// Recovers part of this advance, by shrinking it and handing back a settled tranche for the
    /// part that was taken.
    ///
    /// <para>Called when a month can afford some of an advance but not all of it. See the class
    /// remarks for why the recovered part becomes its own row rather than a partial flag on this
    /// one.</para>
    ///
    /// <para>The tranche inherits this row's <see cref="AdvanceDate"/>, <see cref="Reason"/> and
    /// <see cref="GivenByUserId"/> — it is the same handover, and dating it to the day it was
    /// recovered instead would move an expense into a month the cash never moved in.</para>
    /// </summary>
    /// <param name="amount">
    /// How much to recover. Must be greater than zero and less than <see cref="Amount"/>:
    /// recovering all of it is <see cref="SettleIn"/>'s job, and a caller asking for more than
    /// there is has made an arithmetic error worth throwing over.
    /// </param>
    /// <param name="salaryEntryId">The entry doing the recovering.</param>
    /// <returns>The settled tranche, which the caller must persist.</returns>
    internal SalaryAdvance SplitOff(decimal amount, Guid salaryEntryId)
    {
        if (amount <= 0m || amount >= Amount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                $"A split must recover more than nothing and less than the whole {Amount:0.00}.");
        }

        var recovered = new SalaryAdvance(
            EmployeeSalaryProfileId, amount, AdvanceDate, Reason, GivenByUserId);

        // Born settled. It exists only because this entry recovered it, and an instant in which
        // it is unsettled is an instant in which the employee appears to owe the money twice.
        recovered.SettleIn(salaryEntryId);

        Amount -= amount;

        return recovered;
    }
}
