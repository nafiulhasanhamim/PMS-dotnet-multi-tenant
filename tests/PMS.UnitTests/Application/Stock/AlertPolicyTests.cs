using FluentAssertions;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Stock;
using Xunit;

namespace PMS.UnitTests.Application.Stock;

/// <summary>
/// The two pieces of Module 6 that are decisions rather than SQL: how urgent a row is, and which
/// windows a caller may ask for.
///
/// <para>Everything else in that module is a query, tested against a database by the acceptance
/// harness. These two are worth unit tests because they are read by three screens each — the
/// list, the dashboard card and the API — and a disagreement between them would show up as a row
/// coloured differently from the count that led somebody to it.</para>
/// </summary>
public class AlertPolicyTests
{
    // ── Severity ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]    // Expires today.
    [InlineData(1)]
    [InlineData(7)]    // The boundary is inclusive: seven days left is still critical.
    public void Within_a_week_is_critical(int days)
        => StockPolicy.SeverityFor(days).Should().Be(AlertSeverity.Critical);

    [Theory]
    [InlineData(-1)]
    [InlineData(-400)]
    public void Already_expired_is_critical(int days)
    {
        // Negative days reach this function from the expired list. Nothing is more urgent than
        // stock that is already past its date, so the switch has to fall into the first arm
        // rather than off the end of it.
        StockPolicy.SeverityFor(days).Should().Be(AlertSeverity.Critical);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(30)]   // Also inclusive.
    public void Within_a_month_is_a_warning(int days)
        => StockPolicy.SeverityFor(days).Should().Be(AlertSeverity.Warning);

    [Theory]
    [InlineData(31)]
    [InlineData(90)]
    [InlineData(3650)]
    public void Anything_further_out_is_normal(int days)
        => StockPolicy.SeverityFor(days).Should().Be(AlertSeverity.Normal);

    [Fact]
    public void The_thresholds_are_ordered()
    {
        // A guard against somebody tuning one number without the other: a critical threshold
        // above the warning one would make the warning arm unreachable, silently.
        //
        // Only these two are compared since Module 10 - the expiring-soon window is the
        // pharmacy's now, so "critical < warning < window" is no longer a property of this class
        // and a pharmacy setting a 5-day window is making a strange but legal choice.
        StockPolicy.ExpiryCriticalDays.Should().BeLessThan(StockPolicy.ExpiryWarningDays);
    }

    // ── The window ───────────────────────────────────────────────────────────────────────

    /// <summary>A pharmacy that has not changed the setting. Was the constant before Module 10.</summary>
    private const int Configured = 90;

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(90)]
    [InlineData(180)]
    public void An_offered_window_is_accepted(int days)
        => StockPolicy.CoerceExpiryWindow(days, Configured).Should().Be(days);

    [Fact]
    public void A_pharmacys_own_window_is_accepted_even_when_it_is_not_a_standard_one()
    {
        // The whole point of the setting. A pharmacy on 45 days must be able to ask for 45.
        StockPolicy.CoerceExpiryWindow(45, configuredWindowDays: 45).Should().Be(45);
        StockPolicy.SelectableExpiryWindows(45).Should().Contain(45);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-30)]
    [InlineData(45)]
    [InlineData(100_000)]
    public void Anything_else_falls_back_to_the_configured_window(int? days)
    {
        // Coerced rather than refused. A stale bookmark or a hand-edited query string should show
        // the pharmacy's own window instead of an error — an alert list is something somebody
        // glances at.
        //
        // The upper bound matters too: an unbounded window is a request for every batch in the
        // pharmacy wearing an alert query's clothes.
        StockPolicy.CoerceExpiryWindow(days, Configured).Should().Be(Configured);
    }

    [Fact]
    public void The_fallback_is_always_one_of_the_offered_windows()
    {
        // Otherwise the dropdown opens on a value it does not contain, and the first thing the
        // page does is silently change the window the user is looking at. True for a standard
        // configured window and for an unusual one, which is why SelectableExpiryWindows adds it.
        foreach (var configured in new[] { 30, 90, 180, 45, 1, 3650 })
        {
            StockPolicy.SelectableExpiryWindows(configured).Should().Contain(configured);
        }
    }

    [Fact]
    public void The_offered_windows_are_ascending_and_distinct()
    {
        StockPolicy.SelectableExpiryWindows(Configured)
            .Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();

        // And still ordered once a pharmacy's own window has been folded in.
        StockPolicy.SelectableExpiryWindows(45)
            .Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
    }

    [Fact]
    public void A_dead_stock_threshold_follows_the_same_rule()
    {
        StockPolicy.CoerceDeadStockThreshold(60, Configured).Should().Be(60);
        StockPolicy.CoerceDeadStockThreshold(45, Configured).Should().Be(Configured);
        StockPolicy.CoerceDeadStockThreshold(null, 45).Should().Be(45);
        StockPolicy.CoerceDeadStockThreshold(45, 45).Should().Be(45);
    }
}
