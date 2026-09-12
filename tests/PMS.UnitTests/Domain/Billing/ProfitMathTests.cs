using FluentAssertions;
using PMS.Domain.Billing;
using Xunit;

namespace PMS.UnitTests.Domain.Billing;

/// <summary>
/// The arithmetic behind every figure on every report.
///
/// <para>Each of these is cheap to pin and expensive to discover: a wrong margin does not throw,
/// does not log and does not produce an empty screen. It produces a plausible number that an owner
/// prices their shelves against.</para>
///
/// <para>The acceptance harness proves the same rules end to end against a real database. These
/// tests pin the rules themselves, so that a refactor that changes them fails here — in a second,
/// with a name explaining what broke — rather than at the far end of a fixture.</para>
/// </summary>
public class ProfitMathTests
{
    // ── Line cost ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Line_cost_is_quantity_times_the_batch_price()
        => ProfitMath.LineCost(70, 4.00m).Should().Be(280.00m);

    [Fact]
    public void Line_cost_keeps_a_fractional_per_unit_price_intact()
    {
        // A 200-tablet box bought for 1,000 costs 5 a tablet; a 30-tablet box bought for 500
        // costs 16.6667. The column is DECIMAL(18,4) for exactly this, and rounding it to paisa
        // per line would misstate margin on every sale out of the batch.
        ProfitMath.LineCost(3, 16.6667m).Should().Be(50.0001m);
    }

    // ── Line profit ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Line_profit_is_revenue_less_cost()
        => ProfitMath.LineProfit(800.00m, 70, 4.00m).Should().Be(520.00m);

    [Fact]
    public void Line_profit_falls_when_the_line_carries_a_discount_share()
    {
        // The same line, before and after Module 5 spread a bill-level discount across it. The
        // second figure is the one every report must use; passing LineTotal instead is the single
        // most likely way to make all eight reports wrong at once.
        var undiscounted = ProfitMath.LineProfit(200.00m, 20, 4.00m);
        var discounted = ProfitMath.LineProfit(180.00m, 20, 4.00m);

        undiscounted.Should().Be(120.00m);
        discounted.Should().Be(100.00m);
    }

    [Fact]
    public void Line_profit_can_be_negative()
    {
        // Selling below cost is possible - a deep discount on stock about to expire is a real
        // decision a pharmacy makes - and the report has to be able to say so.
        ProfitMath.LineProfit(30.00m, 10, 4.00m).Should().Be(-10.00m);
    }

    // ── Returns ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_return_gives_back_the_margin_not_the_whole_sale_price()
    {
        // 5 units sold at 10 and returned: 50 refunded, but 20 of stock came back onto the shelf.
        // The pharmacy is out 30, not 50. Subtracting only the refund would treat returned goods
        // as though they had evaporated.
        ProfitMath.ReturnImpact(50.00m, 5, 4.00m).Should().Be(30.00m);
    }

    [Fact]
    public void A_return_at_cost_costs_nothing()
        => ProfitMath.ReturnImpact(40.00m, 10, 4.00m).Should().Be(0.00m);

    [Fact]
    public void A_discounted_sale_returned_in_full_gives_back_less_than_it_cost()
    {
        // The refund follows the discounted price, so a heavily discounted line returned in full
        // is a loss. That is arithmetically right and worth pinning, because it is the case
        // somebody will report as a bug.
        ProfitMath.ReturnImpact(18.00m, 10, 4.00m).Should().Be(-22.00m);
    }

    // ── Net profit ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Net_profit_subtracts_operating_expenses()
        => ProfitMath.NetProfit(650.00m, 200.00m).Should().Be(450.00m);

    [Fact]
    public void Net_profit_equals_gross_profit_while_expenses_are_zero()
    {
        // What the monthly report shows today, because IOperatingExpenses returns zero until
        // Module 9. The page says so on screen; this pins that the arithmetic is an identity
        // rather than a shortcut somebody has to remember to remove.
        ProfitMath.NetProfit(650.00m, 0m).Should().Be(650.00m);
    }

    // ── Margin ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Margin_is_a_percentage_of_revenue()
        => ProfitMath.MarginPercent(590.00m, 1030.00m)
            .Should().BeApproximately(57.2815m, 0.0001m);

    [Fact]
    public void Margin_on_no_revenue_is_zero_not_an_exception()
    {
        // A product type with no sales in the period has no margin. Zero rather than a throw,
        // and zero rather than a null - a blank cell in a column of percentages reads as a
        // missing figure rather than an absent one.
        ProfitMath.MarginPercent(0m, 0m).Should().Be(0m);
    }

    [Fact]
    public void Margin_is_negative_when_the_period_lost_money()
        => ProfitMath.MarginPercent(-10.00m, 100.00m).Should().Be(-10.00m);

    // ── Weighted average cost ────────────────────────────────────────────────────────────

    [Fact]
    public void Average_cost_is_weighted_by_quantity_not_by_batch()
    {
        // The case the stock valuation turns on. Ten units at 1 and one at 100 average 1.09 a
        // unit; averaging the two PRICES would say 50.50 and overstate the shelf fifty-fold.
        ProfitMath.WeightedAverageCost(110.00m, 101)
            .Should().BeApproximately(1.0891m, 0.0001m);
    }

    [Fact]
    public void Average_cost_matches_the_acceptance_fixture()
    {
        // 5 units at 4.00 and 90 at 6.00 is 560 over 95. A plain average of the two batch prices
        // would give 5.00 and value the shelf at 475.
        ProfitMath.WeightedAverageCost(560.00m, 95)
            .Should().BeApproximately(5.8947m, 0.0001m);
    }

    [Fact]
    public void Average_cost_of_nothing_is_zero()
    {
        // The honest answer: there is nothing to have an average cost of. Not a throw, which
        // would take down a valuation page over a product that happens to be out of stock.
        ProfitMath.WeightedAverageCost(0m, 0).Should().Be(0m);
    }

    // ── Nothing rounds ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Nothing_here_rounds()
    {
        // Deliberate, and the reason reports and the till round in different places. Rounding per
        // line and then summing drifts by up to a paisa a line, which on a month of sales is a
        // discrepancy somebody will try to reconcile against a till and cannot.
        ProfitMath.LineCost(3, 0.3333m).Should().Be(0.9999m);
        ProfitMath.LineProfit(1.00m, 3, 0.3333m).Should().Be(0.0001m);
    }

    [Fact]
    public void Summing_unrounded_lines_beats_summing_rounded_ones()
    {
        // Three lines whose costs each land on a half-paisa. Rounded first, they sum to 1.53;
        // summed first, 1.5150 rounds to 1.52. The second is right, and it is what the reports do.
        var costs = new[] { 0.505m, 0.505m, 0.505m };

        var roundedFirst = costs.Sum(c => Math.Round(c, 2, MidpointRounding.AwayFromZero));
        var summedFirst = Math.Round(costs.Sum(), 2, MidpointRounding.AwayFromZero);

        roundedFirst.Should().Be(1.53m);
        summedFirst.Should().Be(1.52m);
    }
}
