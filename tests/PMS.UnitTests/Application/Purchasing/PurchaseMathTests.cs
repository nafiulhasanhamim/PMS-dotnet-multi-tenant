using FluentAssertions;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Purchasing;
using PMS.Application.Interfaces;
using Xunit;

namespace PMS.UnitTests.Application.Purchasing;

/// <summary>
/// The four derived figures Module 4 shows on every screen.
///
/// <para>Each is arithmetic somebody could plausibly write differently at a call site, and each
/// would then disagree between two pages that are supposed to describe the same bill. These pin
/// the definitions; <c>acceptance_purchases.py</c> proves the same rules end to end against a
/// real database.</para>
/// </summary>
public class PurchaseMathTests
{
    // ── Due ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Due_is_the_total_less_what_was_paid_and_returned() =>
        PurchaseMath.Due(610m, 400m, 0m).Should().Be(210m);

    [Fact]
    public void A_return_reduces_what_is_owed()
    {
        // The acceptance fixture: a 610 bill, 400 paid, then 225 of goods sent back.
        PurchaseMath.Due(610m, 400m, 225m).Should().Be(-15m);
    }

    [Fact]
    public void Due_can_go_negative_and_is_not_clamped()
    {
        // A bill paid in full and then returned against leaves the supplier owing the pharmacy.
        // Flooring this at zero would hide a real credit, which is why nothing here does.
        PurchaseMath.Due(100m, 100m, 40m).Should().Be(-40m);
    }

    // ── Status ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_bill_with_nothing_paid_is_unpaid() =>
        PurchaseMath.StatusFor(610m, 0m, 0m).Should().Be(PurchasePaymentStatus.Unpaid);

    [Fact]
    public void A_bill_part_paid_is_partially_paid() =>
        PurchaseMath.StatusFor(610m, 400m, 0m).Should().Be(PurchasePaymentStatus.PartiallyPaid);

    [Fact]
    public void A_bill_paid_in_full_is_paid() =>
        PurchaseMath.StatusFor(610m, 610m, 0m).Should().Be(PurchasePaymentStatus.Paid);

    [Fact]
    public void A_bill_SETTLED_BY_A_RETURN_reads_as_paid_even_though_nothing_was_paid()
    {
        // The case worth pinning. A delivery returned in full owes nothing, and labelling that
        // "Unpaid" would send somebody looking for money to hand over.
        PurchaseMath.StatusFor(610m, 0m, 610m).Should().Be(PurchasePaymentStatus.Paid);
    }

    [Fact]
    public void An_overpaid_bill_is_paid_rather_than_a_fourth_status()
    {
        // The negative figure lives in the Due column, where it belongs. A separate status would
        // need handling on every screen to say what the number already says.
        PurchaseMath.StatusFor(610m, 700m, 0m).Should().Be(PurchasePaymentStatus.Paid);
    }

    // ── Returnable ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Everything_delivered_is_returnable_while_it_is_all_still_there() =>
        PurchaseMath.Returnable(100, 0, 100).Should().Be(100);

    [Fact]
    public void Prior_returns_reduce_what_is_left_on_the_bill() =>
        PurchaseMath.Returnable(100, 50, 50).Should().Be(50);

    [Fact]
    public void THE_BATCH_CAPS_IT_when_stock_has_been_sold()
    {
        // 200 delivered and never returned against, but only 20 left on the shelf. The bill says
        // 200 could go back; the shelf says 20 can. Taking the lower is what stops a return
        // driving a batch negative.
        PurchaseMath.Returnable(200, 0, 20).Should().Be(20);
    }

    [Fact]
    public void Returnable_is_floored_at_zero()
    {
        // Reachable when a batch was adjusted down below what remained returnable on the bill.
        PurchaseMath.Returnable(100, 90, 0).Should().Be(0);
        PurchaseMath.Returnable(100, 120, 50).Should().Be(0);
    }

    [Fact]
    public void Stock_is_named_as_the_limit_only_when_it_actually_is()
    {
        // The screen says which limit applied, because "you already sent most of it back" and
        // "it has been sold" are different problems to solve.
        PurchaseMath.CappedByStock(200, 0, 20).Should().BeTrue();
        PurchaseMath.CappedByStock(100, 50, 50).Should().BeFalse();
    }

    // ── General payments folded into the due ─────────────────────────────────────────────

    [Fact]
    public void A_general_payment_reduces_the_due_it_is_shown_against()
    {
        // 1,000 bill, nothing paid against it, 235 returned, 750 of the account's unallocated
        // payments shown here. This is the case from the screenshots.
        PurchaseMath.Due(1000m, 0m, 235m, 750m).Should().Be(15m);
    }

    [Fact]
    public void A_BILL_COVERED_BY_GENERAL_PAYMENTS_IS_NOT_UNPAID()
    {
        // The defect this fixed: a bill with AmountPaid = 0 badged "Unpaid" on a supplier page
        // that said nothing was owed. Money did reach the supplier; it just did not name a bill.
        PurchaseMath.StatusFor(1000m, 0m, 235m, 750m)
            .Should().Be(PurchasePaymentStatus.PartiallyPaid);

        PurchaseMath.StatusFor(1000m, 0m, 0m, 1000m)
            .Should().Be(PurchasePaymentStatus.Paid);
    }

    [Fact]
    public void With_no_general_payment_nothing_changes()
    {
        // The parameter defaults to zero, so every existing caller keeps its behaviour.
        PurchaseMath.Due(610m, 400m, 0m).Should().Be(PurchaseMath.Due(610m, 400m, 0m, 0m));

        PurchaseMath.StatusFor(610m, 0m, 0m, 0m).Should().Be(PurchasePaymentStatus.Unpaid);
    }

    [Fact]
    public void THE_BILLS_RECONCILE_TO_THE_SUPPLIER_BALANCE()
    {
        // The whole point of the allocation. Taking the two bills from the screenshots:
        //   PUR-000002  610 total, 400 paid, 225 returned  -> due -15, absorbs nothing
        //   PUR-000007  1000 total,  0 paid, 235 returned  -> owed 765, absorbs 750 of the pool
        // and the supplier: 1610 purchased, 460 returned, 1150 paid -> outstanding 0.
        var older = PurchaseMath.Due(610m, 400m, 225m, 0m);
        var newer = PurchaseMath.Due(1000m, 0m, 235m, 750m);

        var supplier = new SupplierBalance(
            TotalPurchased: 1610m, TotalReturned: 460m, TotalPaid: 1150m);

        (older + newer).Should().Be(supplier.Outstanding);
        (older + newer).Should().Be(0m);
    }

    // ── The adjustment tag ───────────────────────────────────────────────────────────────

    [Fact]
    public void A_returns_reason_is_tagged_so_the_batch_history_explains_itself()
    {
        PurchaseMath.AdjustmentReason("near expiry")
            .Should().Be("Purchase return: near expiry");
    }

    [Fact]
    public void The_tag_trims_what_was_typed() =>
        PurchaseMath.AdjustmentReason("  damaged on arrival  ")
            .Should().Be("Purchase return: damaged on arrival");
}

/// <summary>
/// The outstanding-balance formula, which exists in exactly one place.
///
/// <para>It is a computed property on <c>SupplierBalance</c> rather than a method somewhere, so
/// that the supplier list, the supplier page, the purchases list and Module 8's dues report all
/// read the identical subtraction. These tests pin it.</para>
/// </summary>
public class SupplierBalanceTests
{
    [Fact]
    public void Outstanding_is_purchases_less_returns_less_payments() =>
        new SupplierBalance(610m, 0m, 0m).Outstanding.Should().Be(610m);

    [Fact]
    public void The_messy_sequence_from_the_brief_comes_to_minus_sixty_five()
    {
        // Purchase 610 -> pay 400 -> return 225 -> pay 50. The module brief works this through
        // by hand as 610 - 225 - 450, and this is that arithmetic.
        var balance = new SupplierBalance(
            TotalPurchased: 610m, TotalReturned: 225m, TotalPaid: 450m);

        balance.Outstanding.Should().Be(-65m);
        balance.IsOverpaid.Should().BeTrue();
        balance.IsOwing.Should().BeFalse();
    }

    [Fact]
    public void A_general_payment_counts_the_same_as_a_purchase_linked_one()
    {
        // Both are money that went to the supplier. Excluding general payments would be the
        // single most likely way to overstate a pharmacy's debts.
        new SupplierBalance(610m, 0m, 610m).Outstanding.Should().Be(0m);
    }

    [Fact]
    public void Zero_is_settled_rather_than_owing_or_overpaid()
    {
        var settled = new SupplierBalance(100m, 0m, 100m);

        settled.IsOwing.Should().BeFalse();
        settled.IsOverpaid.Should().BeFalse();
        settled.Outstanding.Should().Be(0m);
    }

    // ── Money coming back ────────────────────────────────────────────────────────────────

    [Fact]
    public void A_REFUND_SETTLES_A_CREDIT()
    {
        // The case that prompted this: 610 bought, 225 returned, 450 paid leaves the supplier
        // holding 65 of the pharmacy's money. They hand it back; nobody owes anybody.
        var before = new SupplierBalance(610m, 225m, 450m);

        before.Outstanding.Should().Be(-65m);
        before.CreditAvailable.Should().Be(65m);

        var after = before with { TotalRefunded = 65m };

        after.Outstanding.Should().Be(0m);
        after.IsOverpaid.Should().BeFalse();
        after.IsSettled.Should().BeTrue();
    }

    [Fact]
    public void A_write_off_moves_the_balance_exactly_as_a_refund_does()
    {
        // No cash involved — the distributor closed, or it is too small to chase — but the credit
        // is gone either way, so the balance has to say so.
        var refunded = new SupplierBalance(610m, 225m, 450m, TotalRefunded: 65m);
        var written = new SupplierBalance(610m, 225m, 450m, TotalWrittenOff: 65m);

        refunded.Outstanding.Should().Be(written.Outstanding);
        written.Outstanding.Should().Be(0m);
    }

    [Fact]
    public void The_two_are_kept_apart_even_though_they_move_the_balance_together()
    {
        // A pharmacy reconciling its till has to be able to tell cash from a written-off credit.
        // One sum over both would make that impossible.
        var mixed = new SupplierBalance(
            610m, 225m, 450m, TotalRefunded: 40m, TotalWrittenOff: 25m);

        mixed.TotalRefunded.Should().Be(40m);
        mixed.TotalWrittenOff.Should().Be(25m);
        mixed.Outstanding.Should().Be(0m);
    }

    [Fact]
    public void A_refund_larger_than_the_credit_puts_the_account_back_into_debt()
    {
        // The supplier handed back too much. Warned about, never refused — and the balance says
        // plainly that the pharmacy owes again.
        var after = new SupplierBalance(610m, 225m, 450m, TotalRefunded: 100m);

        after.Outstanding.Should().Be(35m);
        after.IsOwing.Should().BeTrue();
    }

    [Fact]
    public void A_settled_credit_raises_a_bill_back_toward_zero()
    {
        // The per-bill mirror of the balance rule. A bill in credit by 15, then refunded.
        PurchaseMath.Due(610m, 400m, 225m, 0m, 0m).Should().Be(-15m);
        PurchaseMath.Due(610m, 400m, 225m, 0m, 15m).Should().Be(0m);

        PurchaseMath.StatusFor(610m, 400m, 225m, 0m, 15m)
            .Should().Be(PurchasePaymentStatus.Paid);
    }

    [Fact]
    public void CreditAvailable_is_zero_when_nothing_is_owed_back()
    {
        new SupplierBalance(610m, 0m, 400m).CreditAvailable.Should().Be(0m);
        new SupplierBalance(610m, 0m, 610m).CreditAvailable.Should().Be(0m);
    }

    [Fact]
    public void A_supplier_with_no_activity_has_a_zero_balance()
    {
        SupplierBalance.Zero.Outstanding.Should().Be(0m);
        SupplierBalance.Zero.IsOwing.Should().BeFalse();
    }
}
