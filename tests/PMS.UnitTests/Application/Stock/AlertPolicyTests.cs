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
        StockPolicy.ExpiryCriticalDays.Should().BeLessThan(StockPolicy.ExpiryWarningDays);
        StockPolicy.ExpiryWarningDays.Should().BeLessThan(StockPolicy.ExpiringSoonWindowDays);
    }

    // ── The window ───────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(90)]
    [InlineData(180)]
    public void An_offered_window_is_accepted(int days)
        => StockPolicy.CoerceExpiryWindow(days).Should().Be(days);

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-30)]
    [InlineData(45)]
    [InlineData(100_000)]
    public void Anything_else_falls_back_to_the_configured_window(int? days)
    {
        // Coerced rather than refused. The dropdown offers four choices, and a stale bookmark or
        // a hand-edited query string should show the default page instead of an error — an alert
        // list is something somebody glances at.
        //
        // The upper bound matters too: an unbounded window is a request for every batch in the
        // pharmacy wearing an alert query's clothes.
        StockPolicy.CoerceExpiryWindow(days)
            .Should().Be(StockPolicy.ExpiringSoonWindowDays);
    }

    [Fact]
    public void The_default_window_is_one_of_the_offered_ones()
    {
        // Otherwise the dropdown opens on a value it does not contain, and the first thing the
        // page does is silently change the window the user is looking at.
        StockPolicy.SelectableExpiryWindows
            .Should().Contain(StockPolicy.ExpiringSoonWindowDays);
    }

    [Fact]
    public void The_offered_windows_are_ascending_and_distinct()
        => StockPolicy.SelectableExpiryWindows
            .Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
}
