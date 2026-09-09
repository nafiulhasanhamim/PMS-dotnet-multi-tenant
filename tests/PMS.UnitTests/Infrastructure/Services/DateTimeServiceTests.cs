using PMS.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace PMS.UnitTests.Infrastructure.Services;

/// <summary>
/// Tests for DateTimeService — the application clock.
///
/// <para>The clock exposes UTC and nothing else. The tests that asserted a local-time
/// property are gone along with it: a local <c>Now</c> on a shared clock is precisely how
/// local time ends up in a column named <c>...OnUtc</c>, and nothing consumed it.</para>
/// </summary>
public class DateTimeServiceTests
{
    private readonly DateTimeService _service = new();

    [Fact]
    public void UtcNow_ReturnsCurrentUtcTime()
    {
        var before = DateTime.UtcNow;

        var result = _service.UtcNow;

        var after = DateTime.UtcNow;
        result.Should().BeOnOrAfter(before);
        result.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void UtcNow_ReturnsUtcKind()
        => _service.UtcNow.Kind.Should().Be(DateTimeKind.Utc);

    [Fact]
    public void UtcToday_ReturnsUtcKind()
        => _service.UtcToday.Kind.Should().Be(DateTimeKind.Utc);

    [Fact]
    public void UtcToday_IsMidnight()
        => _service.UtcToday.TimeOfDay.Should().Be(TimeSpan.Zero);

    /// <summary>
    /// The regression worth guarding. <c>Today</c> was documented as UTC and implemented as
    /// <see cref="DateTime.Today"/>, which is local midnight — so east of Greenwich it named
    /// tomorrow for the first hours of every local day. Asserting against the UTC date rather
    /// than the local one is what makes this fail if anyone puts that back.
    /// </summary>
    [Fact]
    public void UtcToday_IsTheUtcDate_NotTheLocalOne()
        => _service.UtcToday.Should().Be(DateTime.UtcNow.Date);

    [Fact]
    public void MultipleCalls_ReturnProgressingTime()
    {
        var first = _service.UtcNow;
        Thread.Sleep(10);
        var second = _service.UtcNow;

        second.Should().BeOnOrAfter(first);
    }
}
