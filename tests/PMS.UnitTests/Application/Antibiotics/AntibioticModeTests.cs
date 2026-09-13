using FluentAssertions;
using PMS.Application.Common.Antibiotics;
using PMS.Application.Common.Billing;
using PMS.Application.Common.Settings;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Xunit;

namespace PMS.UnitTests.Application.Antibiotics;

/// <summary>
/// Module 7's three testable rules: who may dispense under which mode, how a date range resolves,
/// and what a sale records when the mode does not demand everything.
///
/// <para>The first of these is the one worth having a table for. It changed an existing
/// unconditional rule in Module 5, it has legal consequences in both directions, and it is read
/// by three separate call sites — so the full role × mode grid is pinned rather than sampled.</para>
/// </summary>
public class AntibioticModeTests
{
    // ── Who may dispense ─────────────────────────────────────────────────────────────────

    [Theory]
    // Off: the default, and everybody sells. This is what most retail pharmacies actually do,
    // and the row that would have been false before Module 7.
    [InlineData(UserRole.Admin, AntibioticPrescriptionMode.Off, true)]
    [InlineData(UserRole.Pharmacist, AntibioticPrescriptionMode.Off, true)]
    [InlineData(UserRole.Employee, AntibioticPrescriptionMode.Off, true)]

    // Optional: details are collected when available; who may sell is unchanged from Off.
    [InlineData(UserRole.Admin, AntibioticPrescriptionMode.Optional, true)]
    [InlineData(UserRole.Pharmacist, AntibioticPrescriptionMode.Optional, true)]
    [InlineData(UserRole.Employee, AntibioticPrescriptionMode.Optional, true)]

    // Required: the only mode that restricts it.
    [InlineData(UserRole.Admin, AntibioticPrescriptionMode.Required, true)]
    [InlineData(UserRole.Pharmacist, AntibioticPrescriptionMode.Required, true)]
    [InlineData(UserRole.Employee, AntibioticPrescriptionMode.Required, false)]
    public void The_full_role_by_mode_grid(
        UserRole role, AntibioticPrescriptionMode mode, bool expected)
        => BillingPolicy.MaySellAntibiotics(role, mode).Should().Be(expected);

    [Fact]
    public void Only_Required_restricts_anybody()
    {
        // Stated as a property as well as a table, because it is the sentence the whole module
        // rests on: if a future edit made Optional restrictive, the table above would be
        // updated to match and this would catch that the meaning had changed.
        foreach (var mode in new[]
        {
            AntibioticPrescriptionMode.Off,
            AntibioticPrescriptionMode.Optional,
        })
        {
            BillingPolicy.MaySellAntibiotics(UserRole.Employee, mode).Should().BeTrue(
                $"an Employee sells antibiotics normally under {mode}");
        }
    }

    [Fact]
    public void The_default_mode_is_the_loosest_one()
    {
        // Not a preference — a deliberate decision documented on the enum. A default of Required
        // would be turned off on a pharmacy's first day, having first had a fake patient name
        // typed in to get past it.
        default(AntibioticPrescriptionMode).Should().Be(AntibioticPrescriptionMode.Off);
    }

    // ── The date range ───────────────────────────────────────────────────────────────────

    private static readonly DateOnly Today = new(2026, 9, 11);

    [Fact]
    public void No_range_means_the_current_month_to_date()
    {
        var (from, to) = RegisterRange.Resolve(null, null, Today);

        from.Should().Be(new DateOnly(2026, 9, 1));
        to.Should().Be(Today);
    }

    [Fact]
    public void One_end_given_fills_in_the_other()
    {
        var (from, to) = RegisterRange.Resolve(new DateOnly(2026, 8, 1), null, Today);

        from.Should().Be(new DateOnly(2026, 8, 1));
        to.Should().Be(Today);
    }

    [Fact]
    public void A_reversed_range_is_swapped_rather_than_refused()
    {
        // Somebody who types the dates the wrong way round wants to see the rows, not a
        // validation message. Refusing would also make the export and the page disagree about
        // what "the filtered set" is.
        var (from, to) = RegisterRange.Resolve(
            new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 1), Today);

        from.Should().Be(new DateOnly(2026, 9, 1));
        to.Should().Be(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public void A_single_day_is_a_valid_range()
    {
        var (from, to) = RegisterRange.Resolve(Today, Today, Today);

        from.Should().Be(Today);
        to.Should().Be(Today);
    }

    // ── What a sale records ──────────────────────────────────────────────────────────────

    private static Sale SaleWithALine()
    {
        var sale = new Sale("INV-000001", Guid.NewGuid(), new DateTime(2026, 9, 11, 8, 0, 0, DateTimeKind.Utc));
        sale.AddLine(Guid.NewGuid(), Guid.NewGuid(), 2, UnitLevel.Base, 50m, 100m);
        return sale;
    }

    [Fact]
    public void A_partial_prescription_is_recorded_rather_than_refused()
    {
        // The Optional case. A cashier may have the doctor's name and nothing else, and that
        // partial record is worth more to a later inspection than a blank row. The entity used
        // to throw here; completeness is now the handler's business, because it depends on a
        // per-tenant setting the entity cannot see.
        var sale = SaleWithALine();

        sale.SetPrescription(null, null, "Dr Karim", null, null, verified: false);

        sale.DoctorName.Should().Be("Dr Karim");
        sale.PatientName.Should().BeNull();
        sale.PrescriptionNumber.Should().BeNull();
        sale.PrescriptionDate.Should().BeNull();
        sale.HasPrescription.Should().BeTrue("a doctor's name alone is still a record");
    }

    [Fact]
    public void Blank_strings_are_stored_as_null_not_as_empty()
    {
        // So the register's "has a prescription" filter and its em-dash rendering both key off
        // one thing. An empty string would count as captured and print as nothing.
        var sale = SaleWithALine();

        sale.SetPrescription("   ", "", "  ", null, null, verified: false);

        sale.PatientName.Should().BeNull();
        sale.PatientPhone.Should().BeNull();
        sale.DoctorName.Should().BeNull();
        sale.HasPrescription.Should().BeFalse();
    }

    [Fact]
    public void A_complete_prescription_is_recorded_in_full()
    {
        var sale = SaleWithALine();

        sale.SetPrescription(
            "Rahim Uddin", "01711-000000", "Dr Karim", "RX-9001",
            new DateOnly(2026, 9, 10), verified: true);

        sale.PatientName.Should().Be("Rahim Uddin");
        sale.PatientPhone.Should().Be("01711-000000");
        sale.DoctorName.Should().Be("Dr Karim");
        sale.PrescriptionNumber.Should().Be("RX-9001");
        sale.PrescriptionDate.Should().Be(new DateOnly(2026, 9, 10));
        sale.PrescriptionVerified.Should().BeTrue();
        sale.HasPrescription.Should().BeTrue();
    }

    [Fact]
    public void A_sale_with_no_prescription_call_reports_none()
        => SaleWithALine().HasPrescription.Should().BeFalse();

    // ── The setting ──────────────────────────────────────────────────────────────────────
    //
    // Module 10 moved this off the Tenant row into AppSettings, where every other per-pharmacy
    // preference lives - see migration 016. The property these cases used to read is gone, so
    // they now check the same thing one level up: what a pharmacy gets when nothing has set it.

    [Fact]
    public void A_new_pharmacy_starts_in_Off()
    {
        // The acceptance criterion, at the level where it is cheapest to check. Off because it
        // is what an unconfigured pharmacy is actually doing; a default claiming otherwise is one
        // somebody turns off on their first day, having first entered a fake patient name to get
        // past it.
        var definition = SettingKeys.Find(SettingKeys.AntibioticPrescriptionMode);

        definition.Should().NotBeNull();
        definition!.Default.Should().Be(nameof(AntibioticPrescriptionMode.Off));
    }

    [Fact]
    public void The_setting_is_stored_by_name_and_every_mode_round_trips()
    {
        // Stored as the enum's NAME rather than its number, so a settings table read by a person
        // during an incident says "Required" instead of "2". That only helps if every name parses
        // back to the mode it came from.
        foreach (var mode in Enum.GetValues<AntibioticPrescriptionMode>())
        {
            Enum.TryParse<AntibioticPrescriptionMode>(mode.ToString(), ignoreCase: true, out var back)
                .Should().BeTrue();

            back.Should().Be(mode);
        }
    }

    [Fact]
    public void A_new_pharmacy_is_still_created_on_trial()
    {
        // What the old mode test also asserted in passing, kept because nothing else covers it.
        var tenant = new Tenant("Probe Pharmacy", "probe-pharmacy");

        tenant.Name.Should().Be("Probe Pharmacy");
        tenant.Status.Should().Be(TenantStatus.Trial, "a new pharmacy starts on trial");
    }
}
