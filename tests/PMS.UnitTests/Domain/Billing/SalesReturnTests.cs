using FluentAssertions;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Xunit;

namespace PMS.UnitTests.Domain.Billing;

/// <summary>
/// The guard that stops a return refunding money against stock that never left the pharmacy.
/// </summary>
public class SalesReturnTests
{
    private static SaleLine LineOf(int quantity, decimal netLineTotal)
    {
        var sale = new Sale("INV-000001", Guid.NewGuid(), DateTime.UtcNow);
        var line = sale.AddLine(
            Guid.NewGuid(), Guid.NewGuid(), quantity, UnitLevel.Base, 1.50m, netLineTotal);

        return line;
    }

    [Fact]
    public void A_return_attaches_itself_to_the_line()
    {
        var line = LineOf(40, 60m);

        var returned = SalesReturn.Record(line, 10, "changed mind", 15m, Guid.NewGuid());

        line.Returns.Should().ContainSingle().Which.Should().BeSameAs(returned);
        line.ReturnedInBaseUnits.Should().Be(10);
        line.ReturnableInBaseUnits.Should().Be(30);
        line.RefundedAmount.Should().Be(15m);
    }

    [Fact]
    public void More_than_was_sold_is_refused()
    {
        var line = LineOf(40, 60m);

        var record = () => SalesReturn.Record(line, 41, "changed mind", 60m, Guid.NewGuid());

        record.Should().Throw<InvalidOperationException>()
            .WithMessage("*40*");
    }

    [Fact]
    public void More_than_remains_after_an_earlier_return_is_refused()
    {
        var line = LineOf(40, 60m);
        SalesReturn.Record(line, 30, "first", 45m, Guid.NewGuid());

        var record = () => SalesReturn.Record(line, 11, "second", 16.50m, Guid.NewGuid());

        record.Should().Throw<InvalidOperationException>()
            .WithMessage("*already returned 30*");
    }

    [Fact]
    public void Returning_the_exact_remainder_is_allowed()
    {
        var line = LineOf(40, 60m);
        SalesReturn.Record(line, 30, "first", 45m, Guid.NewGuid());

        SalesReturn.Record(line, 10, "second", 15m, Guid.NewGuid());

        line.ReturnableInBaseUnits.Should().Be(0);
        line.RefundedAmount.Should().Be(60m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_return_of_nothing_is_refused(int quantity)
    {
        var line = LineOf(40, 60m);

        var record = () => SalesReturn.Record(line, quantity, "why", 0m, Guid.NewGuid());

        record.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_reason_is_required()
    {
        var line = LineOf(40, 60m);

        var record = () => SalesReturn.Record(line, 1, "   ", 1.50m, Guid.NewGuid());

        record.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_negative_refund_is_refused()
    {
        var line = LineOf(40, 60m);

        var record = () => SalesReturn.Record(line, 1, "why", -1m, Guid.NewGuid());

        record.Should().Throw<ArgumentOutOfRangeException>();
    }
}
