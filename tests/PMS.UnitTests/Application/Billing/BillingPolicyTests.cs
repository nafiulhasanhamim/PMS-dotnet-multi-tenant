using FluentAssertions;
using PMS.Application.Common.Billing;
using PMS.Domain.Enums;
using Xunit;

namespace PMS.UnitTests.Application.Billing;

/// <summary>
/// The role discount caps.
///
/// <para>The case worth having a test for is the flat discount. "Employee, maximum 5%" has to
/// mean something for a cashier who types 100 off rather than 5% — otherwise the cap is a
/// formatting preference and the way round it is to press the other button.</para>
/// </summary>
public class BillingPolicyTests
{
    [Theory]
    [InlineData(UserRole.Employee, 5)]
    [InlineData(UserRole.Pharmacist, 10)]
    public void Staff_caps_are_the_documented_percentages(UserRole role, decimal expected)
        => BillingPolicy.MaxDiscountPercentFor(role).Should().Be(expected);

    [Fact]
    public void An_admin_has_no_cap()
        => BillingPolicy.MaxDiscountPercentFor(UserRole.Admin).Should().BeNull();

    [Fact]
    public void A_platform_operator_may_discount_nothing()
    {
        // Unreachable: a platform token carries no tenant claim, so the till refuses it long
        // before this. Zero rather than null so a caller who gets here by accident is refused
        // rather than waved through as unlimited.
        BillingPolicy.MaxDiscountPercentFor(UserRole.PlatformAdmin).Should().Be(0m);
    }

    [Fact]
    public void An_employee_cap_on_a_500_bill_is_25_taka()
    {
        // The brief's example, and the reason the cap is evaluated in taka rather than by
        // discount type.
        BillingPolicy.MaxDiscountAmountFor(UserRole.Employee, 500m).Should().Be(25m);

        BillingPolicy.IsDiscountAllowed(UserRole.Employee, 500m, 25m).Should().BeTrue();
        BillingPolicy.IsDiscountAllowed(UserRole.Employee, 500m, 25.01m).Should().BeFalse();
    }

    [Fact]
    public void A_flat_discount_over_the_percentage_equivalent_is_refused()
    {
        // 100 taka off a 500 bill is 20%, well past an Employee's 5%.
        BillingPolicy.IsDiscountAllowed(UserRole.Employee, 500m, 100m).Should().BeFalse();
        BillingPolicy.IsDiscountAllowed(UserRole.Pharmacist, 500m, 100m).Should().BeFalse();

        // And allowed for an Admin, who has no cap at all.
        BillingPolicy.IsDiscountAllowed(UserRole.Admin, 500m, 100m).Should().BeTrue();
    }

    [Fact]
    public void An_admin_may_take_half_the_bill_off()
        => BillingPolicy.IsDiscountAllowed(UserRole.Admin, 1000m, 500m).Should().BeTrue();

    [Fact]
    public void The_refusal_states_the_callers_own_maximum_both_ways()
    {
        var message = BillingPolicy.DiscountRefusalMessage(UserRole.Pharmacist, 500m);

        // Both figures, because the taka one is what the cashier can act on and the percentage
        // is what explains it. "Too large" on its own means fetching somebody.
        message.Should().Contain("10%").And.Contain("50.00");
    }

    [Fact]
    public void The_cap_description_is_what_the_billing_screen_shows()
    {
        BillingPolicy.DescribeCap(UserRole.Pharmacist).Should().Be("Max discount: 10%");
        BillingPolicy.DescribeCap(UserRole.Admin).Should().Be("No discount limit");
    }
}
