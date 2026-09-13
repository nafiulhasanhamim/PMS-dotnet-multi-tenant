using FluentAssertions;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Xunit;

namespace PMS.UnitTests.Domain.Salary;

/// <summary>
/// The arithmetic of one month's salary, and the settlement rules behind it.
///
/// <para>These are the tests that pin down the module's two genuinely tricky decisions: net
/// payable floors at zero rather than going negative, and advances are settled oldest-first with
/// the last one recovered only in part where necessary. Both are enforced nowhere else — a
/// validator cannot see the advances, and a CHECK constraint can only refuse a negative after the
/// fact.</para>
/// </summary>
public class SalaryEntryTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static SalaryAdvance Advance(decimal amount, int day, int recordedOrder = 0)
    {
        var advance = new SalaryAdvance(
            ProfileId, amount, new DateOnly(2026, 7, day), $"advance of {amount}", UserId);

        // Two advances dated the same day are separated by when they were recorded, which is
        // what makes the order deterministic rather than merely usually right.
        advance.CreatedOnUtc = new DateTime(2026, 7, day, 0, 0, 0, DateTimeKind.Utc)
            .AddMinutes(recordedOrder);

        return advance;
    }

    private static SalaryEntry Generate(
        decimal baseSalary = 15000m,
        decimal bonus = 0m,
        decimal requestedAdvanceDeduction = 0m,
        decimal otherDeduction = 0m,
        string? notes = null,
        SalaryAdvance[]? advances = null) =>
        SalaryEntry.Generate(
            ProfileId, 8, 2026, baseSalary, bonus, requestedAdvanceDeduction, otherDeduction,
            notes, UserId, advances ?? []);

    // ── The plain arithmetic ────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_WithNothingToDeduct_PaysTheBaseSalary()
    {
        var entry = Generate(baseSalary: 15000m);

        entry.NetPayable.Should().Be(15000m);
        entry.AdvanceDeduction.Should().Be(0m);
        entry.PaymentStatus.Should().Be(SalaryPaymentStatus.Unpaid);
        entry.PaymentDate.Should().BeNull();
    }

    [Fact]
    public void Generate_AddsBonusAndSubtractsDeductions()
    {
        var entry = Generate(
            baseSalary: 15000m, bonus: 1000m, otherDeduction: 500m, notes: "Eid bonus");

        entry.NetPayable.Should().Be(15500m);
    }

    [Fact]
    public void Generate_CopiesTheBaseSalaryRatherThanReferencingIt()
    {
        // The profile is not even passed in - only the figure. This is the shape that makes a
        // later raise unable to restate a month that has already happened.
        var entry = Generate(baseSalary: 15000m);

        entry.BaseSalary.Should().Be(15000m);
    }

    [Fact]
    public void Generate_TrimsNotesAndTreatsWhitespaceAsAbsent()
    {
        Generate(notes: "  Eid bonus  ").AdjustmentNotes.Should().Be("Eid bonus");
        Generate(notes: "   ").AdjustmentNotes.Should().BeNull();
    }

    // ── Advance settlement ──────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_SettlesAnAdvanceItCanCover()
    {
        var advance = Advance(2000m, day: 10);

        var entry = Generate(requestedAdvanceDeduction: 2000m, advances: [advance]);

        entry.NetPayable.Should().Be(13000m);
        entry.AdvanceDeduction.Should().Be(2000m);
        advance.IsSettled.Should().BeTrue();
        advance.SettledInSalaryEntryId.Should().Be(entry.Id);
    }

    [Fact]
    public void Generate_SettlesOldestFirst()
    {
        var newer = Advance(3000m, day: 20);
        var older = Advance(2000m, day: 2);

        // Only 2,000 is recoverable, so the ORDER decides which advance it comes out of. Passing
        // them newest-first proves the ordering is the entity's rather than the caller's.
        var entry = Generate(requestedAdvanceDeduction: 2000m, advances: [newer, older]);

        older.IsSettled.Should().BeTrue();
        newer.IsSettled.Should().BeFalse();
        entry.AdvanceDeduction.Should().Be(2000m);
    }

    [Fact]
    public void Generate_BreaksTiesOnTheSameDayByWhenTheAdvanceWasRecorded()
    {
        var second = Advance(1000m, day: 5, recordedOrder: 2);
        var first = Advance(1000m, day: 5, recordedOrder: 1);

        var entry = Generate(requestedAdvanceDeduction: 1000m, advances: [second, first]);

        first.IsSettled.Should().BeTrue();
        second.IsSettled.Should().BeFalse();
        entry.AdvanceDeduction.Should().Be(1000m);
    }

    [Fact]
    public void Generate_RecoversOnlyWhatWasAskedFor()
    {
        var advance = Advance(5000m, day: 3);

        // Partial settlement is a real decision: an owner may recover half now and half later.
        var entry = Generate(requestedAdvanceDeduction: 2000m, advances: [advance]);

        entry.AdvanceDeduction.Should().Be(2000m);
        entry.NetPayable.Should().Be(13000m);
        advance.IsSettled.Should().BeFalse();
        advance.Amount.Should().Be(3000m, "the unrecovered remainder carries forward");
    }

    [Fact]
    public void Generate_IgnoresAdvancesThatAreAlreadySettled()
    {
        var settled = Advance(2000m, day: 1);

        var other = Advance(1000m, day: 2);

        var first = Generate(requestedAdvanceDeduction: 2000m, advances: [settled]);
        first.AdvanceDeduction.Should().Be(2000m);

        // A second month sees the same list. The already-settled row must not be recovered again.
        var second = Generate(requestedAdvanceDeduction: 3000m, advances: [settled, other]);

        second.AdvanceDeduction.Should().Be(1000m);
        other.IsSettled.Should().BeTrue();
    }

    // ── The overshoot rule ──────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_CapsTheDeductionSoNetPayableNeverGoesNegative()
    {
        var advance = Advance(16000m, day: 3);

        var entry = Generate(baseSalary: 15000m, requestedAdvanceDeduction: 16000m,
            advances: [advance]);

        entry.NetPayable.Should().Be(0m, "an employee cannot be asked to hand money back");
        entry.AdvanceDeduction.Should().Be(15000m);
    }

    [Fact]
    public void Generate_WhenOnlyPartOfAnAdvanceFits_SplitsItAndCarriesTheRemainder()
    {
        var advance = Advance(16000m, day: 3);

        var entry = Generate(baseSalary: 15000m, requestedAdvanceDeduction: 16000m,
            advances: [advance]);

        // The original row shrinks to what is still owed...
        advance.Amount.Should().Be(1000m);
        advance.IsSettled.Should().BeFalse();

        // ...and a settled tranche appears for what was recovered.
        entry.NewAdvanceTranches.Should().HaveCount(1);
        entry.NewAdvanceTranches[0].Amount.Should().Be(15000m);
        entry.NewAdvanceTranches[0].IsSettled.Should().BeTrue();
        entry.NewAdvanceTranches[0].SettledInSalaryEntryId.Should().Be(entry.Id);
    }

    [Fact]
    public void Generate_SplitTrancheKeepsTheOriginalAdvanceDate()
    {
        var advance = Advance(16000m, day: 3);

        var entry = Generate(baseSalary: 15000m, requestedAdvanceDeduction: 16000m,
            advances: [advance]);

        // The expense belongs to the day the cash left the register. Dating the tranche to the
        // day it was recovered instead would silently move money into a different month's
        // profit and loss - see IOperatingExpenses.
        entry.NewAdvanceTranches[0].AdvanceDate.Should().Be(new DateOnly(2026, 7, 3));
        entry.NewAdvanceTranches[0].Reason.Should().Be(advance.Reason);
    }

    [Fact]
    public void Generate_ASplitPreservesTheTotalHandedOver()
    {
        var advance = Advance(16000m, day: 3);

        var entry = Generate(baseSalary: 15000m, requestedAdvanceDeduction: 16000m,
            advances: [advance]);

        (advance.Amount + entry.NewAdvanceTranches.Sum(t => t.Amount)).Should().Be(16000m);
    }

    [Fact]
    public void Generate_TheCapAccountsForBonusAndOtherDeduction()
    {
        var advance = Advance(20000m, day: 3);

        // 15,000 + 2,000 bonus - 1,000 other = 16,000 recoverable.
        var entry = Generate(
            baseSalary: 15000m, bonus: 2000m, requestedAdvanceDeduction: 20000m,
            otherDeduction: 1000m, notes: "overtime, less uniform", advances: [advance]);

        entry.AdvanceDeduction.Should().Be(16000m);
        entry.NetPayable.Should().Be(0m);
        advance.Amount.Should().Be(4000m);
    }

    [Fact]
    public void Generate_AnOtherDeductionLargerThanThePayAlsoFloorsAtZero()
    {
        var entry = Generate(baseSalary: 15000m, otherDeduction: 20000m, notes: "a mistake");

        entry.NetPayable.Should().Be(0m);
        entry.AdvanceDeduction.Should().Be(0m);
    }

    [Fact]
    public void CarriedOver_ReportsWhatTheCapCouldNotRecover()
    {
        var advance = Advance(16000m, day: 3);

        var entry = Generate(baseSalary: 15000m, requestedAdvanceDeduction: 16000m,
            advances: [advance]);

        entry.CarriedOver(16000m).Should().Be(1000m);
    }

    // ── Revision ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Revise_LoweringTheDeductionReleasesTheAdvancesItNoLongerRecovers()
    {
        var older = Advance(3000m, day: 5);
        var newer = Advance(4000m, day: 15);

        var entry = Generate(baseSalary: 20000m, requestedAdvanceDeduction: 7000m,
            advances: [older, newer]);

        entry.AdvanceDeduction.Should().Be(7000m);

        entry.Revise(0m, 3000m, 0m, null, [older, newer]);

        entry.AdvanceDeduction.Should().Be(3000m);
        entry.NetPayable.Should().Be(17000m);
        older.IsSettled.Should().BeTrue("oldest first still applies on a revision");
        newer.IsSettled.Should().BeFalse("the released advance goes back in the queue");
    }

    [Fact]
    public void Revise_RaisingTheDeductionTakesMoreBack()
    {
        var older = Advance(3000m, day: 5);
        var newer = Advance(4000m, day: 15);

        var entry = Generate(baseSalary: 20000m, requestedAdvanceDeduction: 3000m,
            advances: [older, newer]);

        entry.Revise(0m, 7000m, 0m, null, [older, newer]);

        entry.AdvanceDeduction.Should().Be(7000m);
        older.IsSettled.Should().BeTrue();
        newer.IsSettled.Should().BeTrue();
    }

    [Fact]
    public void Revise_ClearsTranchesFromAPreviousPassRatherThanAccumulatingThem()
    {
        var advance = Advance(16000m, day: 3);

        var entry = Generate(baseSalary: 15000m, requestedAdvanceDeduction: 16000m,
            advances: [advance]);

        entry.NewAdvanceTranches.Should().HaveCount(1);

        // Nothing to recover this time, so there is nothing new to persist. A stale tranche
        // surviving here would be written to the database a second time.
        entry.Revise(0m, 0m, 0m, null, [advance]);

        entry.NewAdvanceTranches.Should().BeEmpty();
    }

    [Fact]
    public void Revise_OnAPaidEntryThrows()
    {
        var entry = Generate();
        entry.MarkPaid(new DateOnly(2026, 8, 31));

        var act = () => entry.Revise(5000m, 0m, 0m, "a late bonus", []);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*paid*cannot be changed*");
    }

    // ── Payment ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void MarkPaid_RecordsTheDateAndLocksTheEntry()
    {
        var entry = Generate();

        entry.MarkPaid(new DateOnly(2026, 9, 2));

        entry.IsPaid.Should().BeTrue();
        entry.PaymentStatus.Should().Be(SalaryPaymentStatus.Paid);

        // September, for an August salary. Cash basis: the expense follows the money, not the
        // month it covers.
        entry.PaymentDate.Should().Be(new DateOnly(2026, 9, 2));
        entry.Month.Should().Be(8);
    }

    [Fact]
    public void MarkPaid_Twice_Throws()
    {
        var entry = Generate();
        entry.MarkPaid(new DateOnly(2026, 8, 31));

        var act = () => entry.MarkPaid(new DateOnly(2026, 9, 30));

        act.Should().Throw<InvalidOperationException>();
        entry.PaymentDate.Should().Be(new DateOnly(2026, 8, 31),
            "a second attempt must not move which month the expense falls in");
    }
}
