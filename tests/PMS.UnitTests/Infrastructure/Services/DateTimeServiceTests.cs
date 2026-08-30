using PMS.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace PMS.UnitTests.Infrastructure.Services;

/// <summary>
/// Tests for DateTimeService.
/// </summary>
public class DateTimeServiceTests
{
    private readonly DateTimeService _service;

    public DateTimeServiceTests()
    {
        _service = new DateTimeService();
    }

    [Fact]
    public void Now_ReturnsCurrentLocalTime()
    {
        // Arrange
        var before = DateTime.Now;

        // Act
        var result = _service.Now;

        // Assert
        var after = DateTime.Now;
        result.Should().BeOnOrAfter(before);
        result.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void UtcNow_ReturnsCurrentUtcTime()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var result = _service.UtcNow;

        // Assert
        var after = DateTime.UtcNow;
        result.Should().BeOnOrAfter(before);
        result.Should().BeOnOrBefore(after);
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Now_ReturnsLocalKind()
    {
        // Act
        var result = _service.Now;

        // Assert
        result.Kind.Should().Be(DateTimeKind.Local);
    }

    [Fact]
    public void UtcNow_ReturnsUtcKind()
    {
        // Act
        var result = _service.UtcNow;

        // Assert
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void MultipleCalls_ReturnProgressingTime()
    {
        // Act
        var first = _service.UtcNow;
        Thread.Sleep(10); // Small delay
        var second = _service.UtcNow;

        // Assert
        second.Should().BeOnOrAfter(first);
    }
}
