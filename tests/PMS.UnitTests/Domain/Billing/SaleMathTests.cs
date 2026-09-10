using FluentAssertions;
using PMS.Domain.Billing;
using PMS.Domain.Enums;
using Xunit;

namespace PMS.UnitTests.Domain.Billing;

/// <summary>
/// The two calculations Module 5 cannot get wrong.
///
/// <para>Both are cheap to test and expensive to discover in production. A discount split that
/// does not reconcile shows up as a refund that overpays, months later, on one sale in ten; a
/// refund taken off the wrong figure overpays on every discounted sale from the day it ships.
/// Neither produces an error, a log line or a failing page.</para>
/// </summary>
public class SaleMathTests
{
    // ── Rounding ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(13.125, 13.13)]   // The module brief's own figure. ToEven would give 13.12.
    [InlineData(13.135, 13.14)]
    [InlineData(0.005, 0.01)]
    [InlineData(-0.005, -0.01)]
    [InlineData(1.994, 1.99)]
    public void Round_goes_away_from_zero_not_to_even(decimal input, decimal expected) =>
        SaleMath.Round(input).Should().Be(expected);

    [Fact]
    public void Round_is_not_the_dotnet_default()
    {
        // Pinning the difference, so nobody "simplifies" this to Math.Round(x, 2). A customer
        // handed back two paisa less than the arithmetic says is a customer who is right.
        Math.Round(13.125m, 2).Should().Be(13.12m);
        SaleMath.Round(13.125m).Should().Be(13.13m);
    }

    // ── Discount amount ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Percent_discount_is_a_percentage_of_the_subtotal()
        => SaleMath.DiscountAmountFor(1240m, DiscountType.Percent, 5m).Should().Be(62m);

    [Fact]
    public void Flat_discount_is_taken_at_face_value()
        => SaleMath.DiscountAmountFor(160m, DiscountType.Flat, 20m).Should().Be(20m);

    [Fact]
    public void A_discount_can_never_exceed_the_subtotal()
    {
        // A mistyped flat discount must not produce a negative bill and hand money over.
        SaleMath.DiscountAmountFor(160m, DiscountType.Flat, 5000m).Should().Be(160m);
        SaleMath.DiscountAmountFor(160m, DiscountType.Percent, 250m).Should().Be(160m);
    }

    [Fact]
    public void A_hundred_percent_discount_is_the_whole_subtotal()
        => SaleMath.DiscountAmountFor(160m, DiscountType.Percent, 100m).Should().Be(160m);

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_zero_or_negative_discount_is_nothing(decimal value)
        => SaleMath.DiscountAmountFor(160m, DiscountType.Percent, value).Should().Be(0m);

    // ── The worked example from the module brief ─────────────────────────────────────────

    [Fact]
    public void The_worked_example_splits_exactly_as_specified()
    {
        // Napa 40 pieces at 1.50 = 60.00, Azin 2 pieces at 50 = 100.00. Subtotal 160, flat 20.
        var lineTotals = new[] { 60m, 100m };

        var shares = SaleMath.SplitDiscount(lineTotals, 20m);

        shares[0].Should().Be(7.50m, "60/160 of 20");
        shares[1].Should().Be(12.50m, "100/160 of 20");
        shares.Sum().Should().Be(20m, "the shares must reconcile to the discount exactly");

        (lineTotals[0] - shares[0]).Should().Be(52.50m);
        (lineTotals[1] - shares[1]).Should().Be(87.50m);
    }

    // ── The residual ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Three_equal_lines_leave_a_residual_which_the_last_line_absorbs()
    {
        // The brief's example: three lines of 111 with a 10 discount. Each proportional share
        // is 3.3333..., which rounds to 3.33 — and 3.33 three times is 9.99, a paisa short.
        var shares = SaleMath.SplitDiscount(new[] { 111m, 111m, 111m }, 10m);

        shares[0].Should().Be(3.33m);
        shares[1].Should().Be(3.33m);
        shares[2].Should().Be(3.34m, "the last line absorbs the missing paisa");

        shares.Sum().Should().Be(10m);
    }

    [Fact]
    public void The_residual_can_be_negative_and_still_reconciles()
    {
        // Rounding overshoots as readily as it undershoots, which is why the reconciliation is
        // signed. Three lines of 0.05 sharing 0.10: each share rounds up.
        var shares = SaleMath.SplitDiscount(new[] { 0.05m, 0.05m, 0.05m }, 0.10m);

        shares.Sum().Should().Be(0.10m);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(13)]
    [InlineData(97)]
    public void Shares_reconcile_for_any_number_of_awkward_lines(int lineCount)
    {
        // A property rather than an example: whatever the line count and whatever the rounding
        // does per line, the shares sum to the discount. This is the invariant Sale.ApplyDiscount
        // asserts on, and the one a partial return depends on months later.
        var lineTotals = Enumerable.Range(1, lineCount)
            .Select(i => 3.33m * i + 0.07m)
            .ToList();

        var shares = SaleMath.SplitDiscount(lineTotals, 19.99m);

        shares.Sum().Should().Be(19.99m);
    }

    [Fact]
    public void No_discount_means_no_shares()
        => SaleMath.SplitDiscount(new[] { 60m, 100m }, 0m).Should().OnlyContain(share => share == 0m);

    [Fact]
    public void Lines_totalling_zero_do_not_divide_by_zero()
    {
        var shares = SaleMath.SplitDiscount(new[] { 0m, 0m }, 10m);

        shares.Should().OnlyContain(share => share == 0m);
    }

    // ── Splitting a cart item across FEFO batches ────────────────────────────────────────

    [Fact]
    public void A_split_item_line_totals_sum_to_what_the_screen_quoted()
    {
        // 5 pieces of a product priced 10.00 a strip of 3 is 16.67. FEFO takes 4 from one batch
        // and 1 from another. Priced independently that would be 13.33 + 3.33 = 16.66 — a paisa
        // short of the figure the cashier read out, on a bill the customer is holding.
        var lineTotals = SaleMath.SplitLineTotals(16.67m, new[] { 4, 1 });

        lineTotals.Sum().Should().Be(16.67m);
        lineTotals[0].Should().Be(13.34m);
        lineTotals[1].Should().Be(3.33m);
    }

    [Fact]
    public void An_even_split_needs_no_reconciliation()
    {
        // A carton of 24 at 4320 is exactly 180 a bottle, so 10 and 14 divide cleanly.
        var lineTotals = SaleMath.SplitLineTotals(4320m, new[] { 10, 14 });

        lineTotals[0].Should().Be(1800m);
        lineTotals[1].Should().Be(2520m);
        lineTotals.Sum().Should().Be(4320m);
    }

    [Fact]
    public void An_unsplit_item_is_its_whole_total()
        => SaleMath.SplitLineTotals(60m, new[] { 40 }).Should().Equal(60m);

    // ── Refunds ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_full_return_refunds_the_discounted_line_total_not_the_sticker_price()
    {
        // The brief: returning all 40 Napa from the worked example refunds 52.50, not 60.00.
        SaleMath.RefundFor(40, 40, 52.50m).Should().Be(52.50m);
    }

    [Fact]
    public void A_partial_return_refunds_its_proportional_share()
    {
        // 10 of 40 against a net line total of 52.50 is 13.125, rounded away from zero.
        SaleMath.RefundFor(10, 40, 52.50m).Should().Be(13.13m);
    }

    [Fact]
    public void Returning_a_line_in_pieces_still_totals_the_net_line_total()
    {
        // Four returns of ten against a 52.50 line. Naively that is 13.13 four times = 52.52,
        // an overpayment of two paisa — and it would break the property that returning
        // everything on a sale refunds exactly Sale.NetTotal. The completing return pays the
        // remainder instead.
        var netLineTotal = 52.50m;
        var returned = 0;
        var refunded = 0m;

        for (var i = 0; i < 4; i++)
        {
            var refund = SaleMath.RefundFor(10, 40, netLineTotal, returned, refunded);
            returned += 10;
            refunded += refund;
        }

        returned.Should().Be(40);
        refunded.Should().Be(52.50m, "the four refunds must add up to what the customer paid");
    }

    [Fact]
    public void The_completing_return_pays_the_remainder_even_when_it_is_awkward()
    {
        // Three of four returned at 13.13 each is 39.39; the last one pays 13.11, not 13.13.
        var refund = SaleMath.RefundFor(10, 40, 52.50m, alreadyReturnedInBaseUnits: 30,
            alreadyRefunded: 39.39m);

        refund.Should().Be(13.11m);
    }

    [Fact]
    public void An_undiscounted_line_refunds_its_face_value()
        => SaleMath.RefundFor(5, 20, 100m).Should().Be(25m);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_zero_or_negative_return_refunds_nothing(int quantity)
        => SaleMath.RefundFor(quantity, 40, 52.50m).Should().Be(0m);

    [Fact]
    public void A_refund_is_never_negative_even_if_more_was_already_paid_back()
    {
        // Unreachable through the command, which caps the quantity — this is the guard behind
        // it. Paying out a negative refund would mean taking money from the customer.
        SaleMath.RefundFor(10, 40, 52.50m, 30, 60m).Should().Be(0m);
    }

    /// <summary>
    /// The end-to-end property the brief asks for by name: returning everything on the worked
    /// example refunds exactly the sale's net total.
    /// </summary>
    [Fact]
    public void Returning_everything_on_a_discounted_sale_refunds_the_net_total()
    {
        var lineTotals = new[] { 60m, 100m };
        var discountAmount = SaleMath.DiscountAmountFor(160m, DiscountType.Flat, 20m);
        var shares = SaleMath.SplitDiscount(lineTotals, discountAmount);

        var netTotal = 160m - discountAmount;
        netTotal.Should().Be(140m);

        var refunded = 0m;

        for (var i = 0; i < lineTotals.Length; i++)
        {
            var netLineTotal = lineTotals[i] - shares[i];
            refunded += SaleMath.RefundFor(1, 1, netLineTotal);
        }

        refunded.Should().Be(140m);
    }
}
