using FluentAssertions;
using PMS.Application.Common.DTOs;
using Xunit;

namespace PMS.UnitTests.Application.Stock;

/// <summary>
/// What the sidebar badge counts.
///
/// <para>Here rather than in the page test because the badge is cached for a minute per pharmacy,
/// so a rendered number is a statement about the cache as much as about the formula. The formula
/// is a pure property and is pinned where nothing can make it stale.</para>
/// </summary>
public class AlertSummaryTests
{
    private static AlertSummaryDto Summary(
        int expiring = 0, int expired = 0, int low = 0, int outOfStock = 0) =>
        new(expiring, expired, low, outOfStock, 90, null);

    [Fact]
    public void The_urgent_count_is_expired_plus_out_of_stock()
        => Summary(expired: 3, outOfStock: 4).UrgentCount.Should().Be(7);

    [Fact]
    public void It_excludes_expiring_soon_and_merely_low()
    {
        // The distinction the badge exists to make. Expiring stock and low stock are things to
        // plan around this week; expired stock is money already lost on a shelf and an
        // out-of-stock product is a sale being turned away now. Folding all four in would light
        // the badge permanently on a pharmacy that is actually fine, and a badge that is always
        // lit is one nobody reads.
        Summary(expiring: 40, low: 25).UrgentCount.Should().Be(0);
    }

    [Fact]
    public void A_pharmacy_with_nothing_wrong_reports_nothing()
    {
        var quiet = Summary();

        quiet.UrgentCount.Should().Be(0);
        quiet.HasAnything.Should().BeFalse();
    }

    [Fact]
    public void HasAnything_covers_all_four_categories_even_though_the_badge_does_not()
    {
        // The dashboard uses this one to decide whether to say "nothing needs attention", and
        // that sentence would be wrong on a pharmacy with forty batches expiring next month.
        Summary(expiring: 1).HasAnything.Should().BeTrue();
        Summary(low: 1).HasAnything.Should().BeTrue();
        Summary(expired: 1).HasAnything.Should().BeTrue();
        Summary(outOfStock: 1).HasAnything.Should().BeTrue();
    }
}
