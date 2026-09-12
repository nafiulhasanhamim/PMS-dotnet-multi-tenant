using PMS.Domain.Enums;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;

namespace PMS.Domain.Entities;

/// <summary>
/// One employee's salary for one month.
///
/// <para><b><see cref="BaseSalary"/> is copied from the profile at generation, never read live.</b>
/// A raise in October must not restate what somebody was paid in July. Deriving the figure on read
/// would silently rewrite every historical payslip the moment a base salary changed — and a payslip
/// that changes after it was handed over is not a record of anything.</para>
///
/// <para><b>A paid entry is immutable.</b> Once <see cref="PaymentStatus"/> is
/// <see cref="Enums.SalaryPaymentStatus.Paid"/>, nothing here can be edited: not the bonus, not the
/// deductions, not the notes. The money has gone and the employee has a slip. Correcting a paid
/// entry is a deliberate database intervention, not a screen — see the module doc.</para>
///
/// <para><b>Which advances this entry settles is decided here</b>, in <see cref="Generate"/>, and
/// the order is oldest first. See that method.</para>
/// </summary>
public sealed class SalaryEntry : BaseAuditableAggregateRoot<Guid>, ITenantEntity
{
    private readonly List<SalaryAdvance> _newAdvanceTranches = [];

    // EF materialises through this.
    private SalaryEntry()
    {
    }

    private SalaryEntry(
        Guid employeeSalaryProfileId,
        int month,
        int year,
        decimal baseSalary,
        Guid generatedByUserId)
    {
        Id = Guid.NewGuid();
        EmployeeSalaryProfileId = employeeSalaryProfileId;
        Month = month;
        Year = year;
        BaseSalary = baseSalary;
        GeneratedByUserId = generatedByUserId;
        PaymentStatus = SalaryPaymentStatus.Unpaid;
    }

    /// <summary>Stamped by the persistence interceptor on insert; never set by a handler.</summary>
    public Guid TenantId { get; set; }

    public Guid EmployeeSalaryProfileId { get; private set; }

    public EmployeeSalaryProfile EmployeeSalaryProfile { get; private set; } = null!;

    /// <summary>1–12. Unique per profile with <see cref="Year"/>, enforced by an index.</summary>
    public int Month { get; private set; }

    public int Year { get; private set; }

    /// <summary>What they earned that month, frozen. See the class remarks.</summary>
    public decimal BaseSalary { get; private set; }

    public decimal Bonus { get; private set; }

    /// <summary>
    /// Advances recovered in this entry.
    ///
    /// <para>Capped so <see cref="NetPayable"/> cannot go negative — see <see cref="Generate"/>.
    /// The figure here always equals the sum of the advances actually marked settled by this
    /// entry, which is what makes "how much of Karim's advance is still outstanding" answerable.</para>
    /// </summary>
    public decimal AdvanceDeduction { get; private set; }

    public decimal OtherDeduction { get; private set; }

    /// <summary>Explains a bonus or an other-deduction. Asked for whenever either is non-zero.</summary>
    public string? AdjustmentNotes { get; private set; }

    /// <summary>
    /// <c>BaseSalary + Bonus − AdvanceDeduction − OtherDeduction</c>, floored at zero.
    ///
    /// <para>Stored, not computed on read, for the same reason <see cref="BaseSalary"/> is: this is
    /// what was or will be handed over, and it must not move when a profile changes.</para>
    /// </summary>
    public decimal NetPayable { get; private set; }

    public SalaryPaymentStatus PaymentStatus { get; private set; }

    /// <summary>
    /// When the money actually left. Null until paid.
    ///
    /// <para><b>This, not the month, is what the expense report keys on.</b> An August salary paid
    /// on 2 September is a September expense — see <c>IOperatingExpenses</c>.</para>
    /// </summary>
    public DateOnly? PaymentDate { get; private set; }

    public Guid GeneratedByUserId { get; private set; }

    public bool IsPaid => PaymentStatus == SalaryPaymentStatus.Paid;

    /// <summary>
    /// Advance tranches this entry's settlement created, which the caller must add to the
    /// database.
    ///
    /// <para>Not a navigation property and not mapped. A tranche is a new <c>SalaryAdvance</c>
    /// row discovered by splitting an existing one, and nothing about this entry owns it — it
    /// belongs to the employee's advance history. Surfacing it here rather than having the domain
    /// reach for a repository keeps the entity free of persistence, which is the rule the rest of
    /// this layer follows.</para>
    ///
    /// <para>Cleared and repopulated by each <see cref="Revise"/>, so it always describes the most
    /// recent settlement rather than accumulating across edits.</para>
    /// </summary>
    public IReadOnlyList<SalaryAdvance> NewAdvanceTranches => _newAdvanceTranches;

    /// <summary>
    /// Builds a month's entry and settles the advances it covers.
    ///
    /// <para><b>The overshoot rule.</b> When unsettled advances exceed what the month can pay,
    /// <paramref name="requestedAdvanceDeduction"/> is capped at <c>BaseSalary + Bonus −
    /// OtherDeduction</c> so <see cref="NetPayable"/> floors at zero. An employee cannot be asked
    /// to hand money back, and a negative payable would be a number no screen could show
    /// honestly.</para>
    ///
    /// <para><b>Settlement is oldest first, and that order is deliberate.</b> Advances are settled
    /// by <c>AdvanceDate</c>, then by when they were recorded, until the capped deduction is used
    /// up; whatever is left stays unsettled and reappears next month. Oldest-first is the only
    /// order under which an advance cannot be stranded indefinitely by newer ones jumping the
    /// queue, and it is what a person reconciling by hand would do.</para>
    ///
    /// <para><b>The last advance the deduction reaches may be recovered only in part</b>, and at
    /// most one per entry ever is. ৳15,000 recovered from a ৳16,000 advance leaves ৳1,000
    /// outstanding rather than recovering nothing at all — see <see cref="SalaryAdvance.SplitOff"/>
    /// for how the recovered part becomes a settled row of its own. Whole-only settlement would
    /// mean a pharmacy that over-advanced by ৳1 recovered nothing that month, which is not a rule
    /// anybody would defend out loud.</para>
    ///
    /// <para>Any tranches created this way are on <see cref="NewAdvanceTranches"/>, and the caller
    /// must persist them.</para>
    /// </summary>
    /// <param name="unsettledAdvances">
    /// Every unsettled advance for this profile, whatever its date. An advance from two months ago
    /// still counts — see <c>SalaryAdvance</c>.
    /// </param>
    public static SalaryEntry Generate(
        Guid employeeSalaryProfileId,
        int month,
        int year,
        decimal baseSalary,
        decimal bonus,
        decimal requestedAdvanceDeduction,
        decimal otherDeduction,
        string? adjustmentNotes,
        Guid generatedByUserId,
        IReadOnlyCollection<SalaryAdvance> unsettledAdvances)
    {
        var entry = new SalaryEntry(
            employeeSalaryProfileId, month, year, baseSalary, generatedByUserId);

        entry.Apply(bonus, requestedAdvanceDeduction, otherDeduction, adjustmentNotes,
            unsettledAdvances);

        return entry;
    }

    /// <summary>
    /// Re-applies the editable figures to an entry that has not been paid.
    ///
    /// <para>Re-runs settlement, so the advances marked against this entry always sum to its
    /// <see cref="AdvanceDeduction"/>. Editing the deduction down without doing that would leave
    /// advances marked settled that nothing had actually recovered.</para>
    /// </summary>
    /// <param name="candidateAdvances">
    /// Everything this entry could settle: its own currently-settled advances plus anything still
    /// unsettled for the profile. The caller loads both because settlement can move either way.
    /// </param>
    /// <remarks>
    /// Check <see cref="NewAdvanceTranches"/> afterwards and persist anything on it.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The entry has been paid.</exception>
    public void Revise(
        decimal bonus,
        decimal requestedAdvanceDeduction,
        decimal otherDeduction,
        string? adjustmentNotes,
        IReadOnlyCollection<SalaryAdvance> candidateAdvances)
    {
        if (IsPaid)
        {
            throw new InvalidOperationException(
                $"Salary for {Month}/{Year} has been paid and cannot be changed.");
        }

        // Released first, so a reduced deduction genuinely frees advances back up rather than
        // leaving them marked settled against money no longer being recovered.
        foreach (var advance in candidateAdvances.Where(a => a.SettledInSalaryEntryId == Id))
        {
            advance.Release();
        }

        Apply(bonus, requestedAdvanceDeduction, otherDeduction, adjustmentNotes,
            candidateAdvances);
    }

    /// <summary>Records payment. The entry is immutable from here.</summary>
    /// <exception cref="InvalidOperationException">Already paid.</exception>
    public void MarkPaid(DateOnly paymentDate)
    {
        if (IsPaid)
        {
            throw new InvalidOperationException(
                $"Salary for {Month}/{Year} was already marked paid on {PaymentDate}.");
        }

        PaymentStatus = SalaryPaymentStatus.Paid;
        PaymentDate = paymentDate;
    }

    /// <summary>
    /// What the capped deduction left unrecovered — the figure the generation screen warns about
    /// and which carries into next month. Derived, not stored: it is a property of this month's
    /// arithmetic, and next month's generation reads the advances themselves.
    /// </summary>
    public decimal CarriedOver(decimal requestedAdvanceDeduction) =>
        Math.Max(0m, requestedAdvanceDeduction - AdvanceDeduction);


    private void Apply(
        decimal bonus,
        decimal requestedAdvanceDeduction,
        decimal otherDeduction,
        string? adjustmentNotes,
        IReadOnlyCollection<SalaryAdvance> advances)
    {
        Bonus = bonus;
        OtherDeduction = otherDeduction;
        AdjustmentNotes = string.IsNullOrWhiteSpace(adjustmentNotes)
            ? null
            : adjustmentNotes.Trim();

        // What is available to recover an advance out of. Negative is possible in principle — a
        // fanciful other-deduction larger than the salary — and clamping it keeps the cap sane.
        var payable = Math.Max(0m, BaseSalary + bonus - otherDeduction);

        _newAdvanceTranches.Clear();

        // What this month may recover: the smaller of what was asked for and what it can bear.
        var recoverable = Math.Min(Math.Max(0m, requestedAdvanceDeduction), payable);
        var settled = 0m;

        // Oldest first. See the Generate remarks for why the order matters.
        foreach (var advance in advances
            .Where(a => !a.IsSettled)
            .OrderBy(a => a.AdvanceDate)
            .ThenBy(a => a.CreatedOnUtc))
        {
            var room = recoverable - settled;

            if (room <= 0m)
            {
                // Nothing left to recover with. Everything from here stays unsettled and appears
                // on next month's generation screen.
                break;
            }

            if (advance.Amount <= room)
            {
                advance.SettleIn(Id);
                settled += advance.Amount;
                continue;
            }

            // Only part of this one fits. It shrinks to the remainder and hands back a settled
            // tranche for what was taken - which is what stops a single over-large advance from
            // blocking recovery entirely. At most one advance per entry lands here, because the
            // room is exhausted by definition.
            _newAdvanceTranches.Add(advance.SplitOff(room, Id));
            settled += room;

            break;
        }

        // The deduction IS what was settled, so the two can never disagree. Asking to deduct more
        // than the month can bear recovers only what it could, and this says so.
        AdvanceDeduction = settled;
        NetPayable = Math.Max(0m, BaseSalary + bonus - settled - otherDeduction);
    }
}
