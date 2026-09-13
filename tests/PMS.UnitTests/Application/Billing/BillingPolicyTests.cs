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
///
/// <para><b>Module 10 made the percentages per-pharmacy</b>, so they arrive as an argument. These
/// tests pass them explicitly, which is rather the point: the rule stays a pure function of its
/// caps and can be checked without a database. The figures below are still 5 and 10 because those
/// are the defaults every pharmacy was seeded with — but they are inputs to the rule now rather
/// than part of it, which is what the last three cases check.</para>
/// </summary>
public class BillingPolicyTests
{
    /// <summary>The seeded defaults, and what an unconfigured pharmacy still gets.</summary>
    private static readonly DiscountCaps Default =
        new(EmployeePercent: 5m, PharmacistPercent: 10m);

    [Theory]
    [InlineData(UserRole.Employee, 5)]
    [InlineData(UserRole.Pharmacist, 10)]
    public void Staff_caps_come_from_the_pharmacys_settings(UserRole role, decimal expected)
        => BillingPolicy.MaxDiscountPercentFor(role, Default).Should().Be(expected);

    [Fact]
    public void An_admin_has_no_cap()
        => BillingPolicy.MaxDiscountPercentFor(UserRole.Admin, Default).Should().BeNull();

    [Fact]
    public void An_admin_is_uncapped_whatever_the_pharmacy_configures()
    {
        // There is deliberately no admin setting: a cap an owner can lift themselves is not a
        // control, so no configuration can produce one.
        var strict = new DiscountCaps(EmployeePercent: 0m, PharmacistPercent: 0m);

        BillingPolicy.MaxDiscountPercentFor(UserRole.Admin, strict).Should().BeNull();
        BillingPolicy.IsDiscountAllowed(UserRole.Admin, 1000m, 1000m, strict).Should().BeTrue();
    }

    [Fact]
    public void A_platform_operator_may_discount_nothing()
    {
        // Unreachable: a platform token carries no tenant claim, so the till refuses it long
        // before this. Zero rather than null so a caller who gets here by accident is refused
        // rather than waved through as unlimited.
        BillingPolicy.MaxDiscountPercentFor(UserRole.PlatformAdmin, Default).Should().Be(0m);
    }

    [Fact]
    public void An_employee_cap_on_a_500_bill_is_25_taka()
    {
        // The brief's example, and the reason the cap is evaluated in taka rather than by
        // discount type.
        BillingPolicy.MaxDiscountAmountFor(UserRole.Employee, 500m, Default).Should().Be(25m);

        BillingPolicy.IsDiscountAllowed(UserRole.Employee, 500m, 25m, Default).Should().BeTrue();
        BillingPolicy.IsDiscountAllowed(UserRole.Employee, 500m, 25.01m, Default)
            .Should().BeFalse();
    }

    [Fact]
    public void A_flat_discount_over_the_percentage_equivalent_is_refused()
    {
        // 100 taka off a 500 bill is 20%, well past an Employee's 5%.
        BillingPolicy.IsDiscountAllowed(UserRole.Employee, 500m, 100m, Default)
            .Should().BeFalse();
        BillingPolicy.IsDiscountAllowed(UserRole.Pharmacist, 500m, 100m, Default)
            .Should().BeFalse();

        // And allowed for an Admin, who has no cap at all.
        BillingPolicy.IsDiscountAllowed(UserRole.Admin, 500m, 100m, Default).Should().BeTrue();
    }

    [Fact]
    public void An_admin_may_take_half_the_bill_off()
        => BillingPolicy.IsDiscountAllowed(UserRole.Admin, 1000m, 500m, Default).Should().BeTrue();

    [Fact]
    public void The_refusal_states_the_callers_own_maximum_both_ways()
    {
        var message = BillingPolicy.DiscountRefusalMessage(UserRole.Pharmacist, 500m, Default);

        // Both figures, because the taka one is what the cashier can act on and the percentage
        // is what explains it. "Too large" on its own means fetching somebody.
        message.Should().Contain("10%").And.Contain("50.00");
    }

    [Fact]
    public void The_cap_description_is_what_the_billing_screen_shows()
    {
        BillingPolicy.DescribeCap(UserRole.Pharmacist, Default).Should().Be("Max discount: 10%");
        BillingPolicy.DescribeCap(UserRole.Admin, Default).Should().Be("No discount limit");
    }

    // ── The caps are genuinely configurable, not decorative ─────────────────────────────

    [Fact]
    public void A_raised_employee_cap_allows_what_the_default_refused()
    {
        // Module 10's own acceptance test, at the level of the rule: 8% of a 500 bill is 40,
        // refused under the seeded 5% and allowed once the pharmacy raises it.
        var raised = Default with { EmployeePercent = 8m };

        BillingPolicy.IsDiscountAllowed(UserRole.Employee, 500m, 40m, Default).Should().BeFalse();
        BillingPolicy.IsDiscountAllowed(UserRole.Employee, 500m, 40m, raised).Should().BeTrue();
    }

    [Fact]
    public void The_helper_text_and_the_refusal_both_follow_the_configured_cap()
    {
        // These two and the server-side check are the same rule read three times. A pharmacy that
        // changed its cap and then got a refusal quoting the old one would be the worst of both.
        var raised = Default with { PharmacistPercent = 25m };

        BillingPolicy.DescribeCap(UserRole.Pharmacist, raised).Should().Be("Max discount: 25%");

        BillingPolicy.DiscountRefusalMessage(UserRole.Pharmacist, 500m, raised)
            .Should().Contain("25%").And.Contain("125.00");
    }

    [Fact]
    public void A_zero_cap_refuses_every_discount_rather_than_none_at_all()
    {
        // Zero is a coherent choice — a pharmacy where only the owner discounts — and the
        // validator allows it for that reason. It must mean "nothing", not "unlimited".
        var none = new DiscountCaps(EmployeePercent: 0m, PharmacistPercent: 0m);

        BillingPolicy.MaxDiscountAmountFor(UserRole.Employee, 500m, none).Should().Be(0m);
        BillingPolicy.IsDiscountAllowed(UserRole.Employee, 500m, 0m, none).Should().BeTrue();
        BillingPolicy.IsDiscountAllowed(UserRole.Employee, 500m, 0.01m, none).Should().BeFalse();
    }
}
