using FluentValidation;
using PMS.Application.Common.Settings;

namespace PMS.Application.Features.Settings.Commands.UpdateSettings;

/// <summary>
/// <para><b>Bounds come from <see cref="SettingKeys"/>, not from literals here.</b> The catalogue
/// already declares each setting's kind, minimum and maximum so that the seed, the API contract
/// and this validator cannot disagree about what a valid expiry window is.</para>
///
/// <para>Every rule is conditional on the field being supplied, because null means "unchanged" —
/// a partial update must not be refused for a field it never mentioned.</para>
/// </summary>
public sealed class UpdateSettingsCommandValidator : AbstractValidator<UpdateSettingsCommand>
{
    public UpdateSettingsCommandValidator()
    {
        // ── Pharmacy details ─────────────────────────────────────────────────────────────
        //
        // Name and phone are required because they appear on every invoice and every salary
        // slip. A receipt with no pharmacy name is not a receipt, and one with no phone number
        // is one a customer cannot act on.
        RuleFor(c => c.PharmacyName)
            .NotEmpty().WithMessage("The pharmacy needs a name - it appears on every invoice.")
            .MaximumLength(200)
            .When(c => c.PharmacyName is not null);

        RuleFor(c => c.PharmacyPhone)
            .NotEmpty().WithMessage("A phone number is required - it appears on every invoice.")
            .MaximumLength(100)
            .When(c => c.PharmacyPhone is not null);

        RuleFor(c => c.PharmacyAddress)
            .MaximumLength(500)
            .When(c => c.PharmacyAddress is not null);

        // Optional: plenty of shops are trading before their licence number is to hand.
        RuleFor(c => c.PharmacyLicenseNumber)
            .MaximumLength(100)
            .When(c => c.PharmacyLicenseNumber is not null);

        // ── Day thresholds ───────────────────────────────────────────────────────────────
        //
        // Zero or negative is not a narrower window, it is a broken one: an expiry alert with a
        // window of zero silently reports nothing, which looks exactly like the feature being
        // off. The upper bound guards the mirror image - ten years includes every batch the
        // pharmacy will ever hold, which looks exactly like the feature being broken.
        RuleFor(c => c.ExpiryAlertWindowDays)
            .InclusiveBetween(1, SettingKeys.MaxDays)
            .WithMessage($"Enter a number of days between 1 and {SettingKeys.MaxDays}.")
            .When(c => c.ExpiryAlertWindowDays is not null);

        RuleFor(c => c.DeadStockThresholdDays)
            .InclusiveBetween(1, SettingKeys.MaxDays)
            .WithMessage($"Enter a number of days between 1 and {SettingKeys.MaxDays}.")
            .When(c => c.DeadStockThresholdDays is not null);

        // ── Reorder level ────────────────────────────────────────────────────────────────
        RuleFor(c => c.DefaultReorderLevel)
            .GreaterThan(0).WithMessage("Enter a reorder level greater than zero.")
            .LessThanOrEqualTo(1_000_000)
            .When(c => c.DefaultReorderLevel is not null);

        // ── Discount caps ────────────────────────────────────────────────────────────────
        //
        // Zero IS allowed, unlike the day thresholds: a pharmacy that lets no one below Admin
        // discount anything is making a coherent choice, and refusing it would force them to
        // set 1% and hope.
        RuleFor(c => c.DiscountCapEmployeePercent)
            .InclusiveBetween(0, 100).WithMessage("Enter a percentage between 0 and 100.")
            .When(c => c.DiscountCapEmployeePercent is not null);

        RuleFor(c => c.DiscountCapPharmacistPercent)
            .InclusiveBetween(0, 100).WithMessage("Enter a percentage between 0 and 100.")
            .When(c => c.DiscountCapPharmacistPercent is not null);

        // NOTE: there is deliberately no rule that a Pharmacist's cap must exceed an Employee's.
        // It is the sensible arrangement and not the only defensible one - a pharmacy whose
        // pharmacist is a locum and whose employee is the owner's family would set them the
        // other way round, and refusing that would be this system inventing a policy.

        // ── Antibiotic mode ──────────────────────────────────────────────────────────────
        //
        // The enum binds from an integer, so a value outside the three is a hand-crafted request
        // rather than a form mistake. Caught here so it reads as a validation failure instead of
        // reaching the settings table as a number no screen can render.
        RuleFor(c => c.AntibioticPrescriptionMode)
            .IsInEnum().WithMessage("Choose Off, Optional or Required.")
            .When(c => c.AntibioticPrescriptionMode is not null);
    }
}
