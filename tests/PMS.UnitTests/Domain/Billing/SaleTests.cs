using FluentAssertions;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Xunit;

namespace PMS.UnitTests.Domain.Billing;

/// <summary>
/// The Sale aggregate: the order the steps have to happen in, and the invariants that hold
/// afterwards.
/// </summary>
public class SaleTests
{
    private static Sale NewSale() =>
        new("INV-000452", Guid.NewGuid(), new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc));

    private static Sale WorkedExample()
    {
        // The brief's example: Napa 40 pieces at 1.50 = 60, Azin 2 pieces at 50 = 100.
        var sale = NewSale();
        sale.AddLine(Guid.NewGuid(), Guid.NewGuid(), 40, UnitLevel.Base, 1.50m, 60m);
        sale.AddLine(Guid.NewGuid(), Guid.NewGuid(), 2, UnitLevel.Base, 50m, 100m);
        return sale;
    }

    [Fact]
    public void Adding_lines_accumulates_the_subtotal()
        => WorkedExample().Subtotal.Should().Be(160m);

    [Fact]
    public void The_worked_example_produces_the_specified_shares_and_nets()
    {
        var sale = WorkedExample();

        sale.ApplyDiscount(DiscountType.Flat, 20m);

        sale.DiscountAmount.Should().Be(20m);
        sale.Lines.Select(line => line.DiscountShare).Should().Equal(7.50m, 12.50m);
        sale.Lines.Select(line => line.NetLineTotal).Should().Equal(52.50m, 87.50m);
    }

    [Fact]
    public void Settling_computes_the_net_and_the_change()
    {
        var sale = WorkedExample();
        sale.ApplyDiscount(DiscountType.Flat, 20m);

        sale.Settle(200m);

        sale.NetTotal.Should().Be(140m);
        sale.CashReceived.Should().Be(200m);
        sale.ChangeGiven.Should().Be(60m);
    }

    [Fact]
    public void A_sale_with_no_discount_leaves_every_line_at_its_face_value()
    {
        var sale = WorkedExample();

        sale.Settle(160m);

        sale.DiscountType.Should().BeNull();
        sale.DiscountAmount.Should().Be(0m);
        sale.NetTotal.Should().Be(160m);
        sale.Lines.Should().OnlyContain(line => line.DiscountShare == 0m);
        sale.Lines.Select(line => line.NetLineTotal).Should().Equal(60m, 100m);
    }

    [Fact]
    public void Not_enough_cash_is_refused_rather_than_recorded_as_a_part_payment()
    {
        var sale = WorkedExample();
        sale.ApplyDiscount(DiscountType.Flat, 20m);

        var settle = () => sale.Settle(100m);

        settle.Should().Throw<InvalidOperationException>()
            .WithMessage("*less than the 140*");
    }

    [Fact]
    public void A_line_cannot_be_added_after_the_discount_is_applied()
    {
        // The fixed order exists so the discount can be split across a known set of lines. A
        // line added afterwards would have no share, and the shares would stop summing to the
        // discount — which a partial return would then quietly get wrong.
        var sale = WorkedExample();
        sale.ApplyDiscount(DiscountType.Flat, 20m);

        var add = () => sale.AddLine(Guid.NewGuid(), Guid.NewGuid(), 1, UnitLevel.Base, 5m, 5m);

        add.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be added after*");
    }

    [Fact]
    public void A_discount_needs_something_to_come_off()
    {
        var apply = () => NewSale().ApplyDiscount(DiscountType.Percent, 5m);

        apply.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_sale_with_no_lines_cannot_be_settled()
    {
        var settle = () => NewSale().Settle(100m);

        settle.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_discount_larger_than_the_bill_is_capped_at_the_bill()
    {
        var sale = WorkedExample();

        sale.ApplyDiscount(DiscountType.Flat, 5000m);

        sale.DiscountAmount.Should().Be(160m);
        sale.Lines.Sum(line => line.DiscountShare).Should().Be(160m);

        sale.Settle(0m);
        sale.NetTotal.Should().Be(0m);
        sale.ChangeGiven.Should().Be(0m);
    }

    [Fact]
    public void Cancelling_records_who_why_and_when()
    {
        var sale = WorkedExample();
        sale.Settle(160m);

        var admin = Guid.NewGuid();
        var at = new DateTime(2026, 9, 11, 4, 0, 0, DateTimeKind.Utc);

        sale.Cancel("wrong customer", admin, at);

        sale.Status.Should().Be(SaleStatus.Cancelled);
        sale.CancelledReason.Should().Be("wrong customer");
        sale.CancelledByUserId.Should().Be(admin);
        sale.CancelledAt.Should().Be(at);
    }

    [Fact]
    public void Cancelling_twice_is_refused_because_it_would_restore_the_stock_twice()
    {
        var sale = WorkedExample();
        sale.Settle(160m);
        sale.Cancel("first", Guid.NewGuid(), DateTime.UtcNow);

        var again = () => sale.Cancel("second", Guid.NewGuid(), DateTime.UtcNow);

        again.Should().Throw<InvalidOperationException>()
            .WithMessage("*already cancelled*");
    }

    [Fact]
    public void A_sale_with_no_prescription_reports_none()
    {
        var sale = WorkedExample();

        sale.HasPrescription.Should().BeFalse();
    }

    [Fact]
    public void Prescription_details_are_recorded_as_given()
    {
        var sale = WorkedExample();

        sale.SetPrescription(
            "Rahim Uddin", "01711-000000", "Dr Karim", "RX-9001",
            new DateOnly(2026, 9, 9), verified: true);

        sale.HasPrescription.Should().BeTrue();
        sale.PatientName.Should().Be("Rahim Uddin");
        sale.PrescriptionNumber.Should().Be("RX-9001");
        sale.PrescriptionVerified.Should().BeTrue();
    }

    [Fact]
    public void A_line_has_to_move_at_least_one_unit()
    {
        var sale = NewSale();

        var add = () => sale.AddLine(Guid.NewGuid(), Guid.NewGuid(), 0, UnitLevel.Base, 5m, 0m);

        add.Should().Throw<ArgumentOutOfRangeException>();
    }
}
