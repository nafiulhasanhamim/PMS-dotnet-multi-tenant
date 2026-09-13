using PMS.Domain.Enums;
using FluentValidation;

namespace PMS.Application.Common.Products;

/// <summary>
/// The fields every product write carries. Implemented by both the create and update commands
/// so the rules below can be written once — two copies of these would drift, and the ones
/// that matter here are the ones that keep nonsense out of the catalogue.
/// </summary>
public interface IProductWriteRequest
{
    ProductType ProductType { get; }

    string BrandName { get; }

    string? Company { get; }

    string? Category { get; }

    string? GenericName { get; }

    string? Strength { get; }

    string? DosageForm { get; }

    bool IsAntibiotic { get; }

    string BaseUnitName { get; }

    string? MidUnitName { get; }

    string? LargeUnitName { get; }

    int? BasePerMid { get; }

    int? MidPerLarge { get; }

    /// <summary>Null when the product has not been priced yet — see the bulk import.</summary>
    decimal? PricePerBase { get; }

    decimal? PricePerMid { get; }

    decimal? PricePerLarge { get; }

    int ReorderLevel { get; }

    string? ShelfLocation { get; }
}

/// <summary>
/// Validation shared by creating and updating a product.
///
/// <para>Two rule groups matter more than the rest. <b>Type-conditional</b> rules keep a
/// diaper from carrying a strength or an antibiotic flag — the exact data corruption this
/// module exists to prevent, and worth rejecting rather than quietly nulling, so a caller
/// sending them learns it was wrong. <b>Unit</b> rules keep a pack count without its level,
/// or a level without its count, out of the database, because either one produces a product
/// whose quantities cannot be interpreted.</para>
/// </summary>
public static class ProductWriteRules
{
    public static void ApplyTo<T>(AbstractValidator<T> validator)
        where T : IProductWriteRequest
    {
        // ── Always ──────────────────────────────────────────────────────────────────────

        validator.RuleFor(x => x.BrandName)
            .NotEmpty().WithMessage("Enter a name.")
            .MaximumLength(200);

        validator.RuleFor(x => x.Company).MaximumLength(200);
        validator.RuleFor(x => x.Category).MaximumLength(100);
        validator.RuleFor(x => x.ShelfLocation).MaximumLength(100);

        // Non-negative *when present*. Whether it may be absent at all is decided by each
        // command: the single-product forms require it, and only the bulk import allows it to
        // be omitted. Putting NotNull here would forbid the whole point of that feature.
        validator.RuleFor(x => x.PricePerBase)
            .GreaterThanOrEqualTo(0).WithMessage("A price cannot be negative.")
            .When(x => x.PricePerBase is not null);

        validator.RuleFor(x => x.ReorderLevel)
            .GreaterThanOrEqualTo(0).WithMessage("A reorder level cannot be negative.");

        // ── Medicine-only fields ────────────────────────────────────────────────────────

        validator.When(x => x.ProductType == ProductType.Medicine, () =>
        {
            validator.RuleFor(x => x.GenericName)
                .NotEmpty().WithMessage("A medicine needs a generic name.")
                .MaximumLength(300);

            validator.RuleFor(x => x.DosageForm)
                .NotEmpty().WithMessage("A medicine needs a dosage form.")
                .MaximumLength(100);

            validator.RuleFor(x => x.Strength).MaximumLength(100);
        });

        validator.When(x => x.ProductType != ProductType.Medicine, () =>
        {
            // Rejected, not silently cleared. A caller sending a strength for a diaper has
            // misunderstood something, and a 400 says so where a quiet null does not.
            validator.RuleFor(x => x.GenericName)
                .Empty().WithMessage("Only a medicine can have a generic name.");

            validator.RuleFor(x => x.Strength)
                .Empty().WithMessage("Only a medicine can have a strength.");

            validator.RuleFor(x => x.DosageForm)
                .Empty().WithMessage("Only a medicine can have a dosage form.");

            validator.RuleFor(x => x.IsAntibiotic)
                .Equal(false).WithMessage("Only a medicine can be marked as an antibiotic.");
        });

        // ── Unit configuration ──────────────────────────────────────────────────────────
        //
        // Valid shapes: base; base + mid; base + large; base + mid + large.

        validator.RuleFor(x => x.BaseUnitName)
            .NotEmpty().WithMessage("Enter the smallest unit you sell.")
            .MaximumLength(50);

        validator.RuleFor(x => x.MidUnitName).MaximumLength(50);
        validator.RuleFor(x => x.LargeUnitName).MaximumLength(50);

        validator.When(x => !string.IsNullOrWhiteSpace(x.MidUnitName), () =>
        {
            // Two separate RuleFor calls, not one chain with a trailing .When().
            //
            // In FluentValidation a trailing .When() applies to EVERY rule in the chain, not
            // just the last one. Written as a single chain ending
            // `.When(x => x.BasePerMid is not null)`, the NotNull was skipped precisely when
            // the value was null — so a pack name with no count sailed through with a 201.
            // Caught by the acceptance run, which is the only reason it is not still there.
            validator.RuleFor(x => x.BasePerMid)
                .NotNull().WithMessage("Enter how many units are in one pack.");

            validator.RuleFor(x => x.BasePerMid)
                .GreaterThan(1).WithMessage("A pack has to hold more than one unit.")
                .When(x => x.BasePerMid is not null);

            // Required only when the product is being priced at all. Either you are setting
            // prices - in which case every level the product has needs one, or the product
            // would be sellable at some levels and not others - or you are not, which is the
            // bulk-import path and leaves the product honestly incomplete. A half-priced
            // product is the state worth forbidding, and this is what forbids it.
            validator.RuleFor(x => x.PricePerMid)
                .NotNull().WithMessage("Enter the price for this pack.")
                .When(x => x.PricePerBase is not null);

            validator.RuleFor(x => x.PricePerMid)
                .GreaterThanOrEqualTo(0).WithMessage("A price cannot be negative.")
                .When(x => x.PricePerMid is not null);
        });

        validator.When(x => string.IsNullOrWhiteSpace(x.MidUnitName), () =>
        {
            // A count with no level is a number nothing can interpret.
            validator.RuleFor(x => x.BasePerMid)
                .Null().WithMessage("Remove the pack count, or name the pack.");

            validator.RuleFor(x => x.PricePerMid)
                .Null().WithMessage("Remove the pack price, or name the pack.");
        });

        validator.When(x => !string.IsNullOrWhiteSpace(x.LargeUnitName), () =>
        {
            // MidPerLarge counts MID units when a mid level exists, and BASE units when it
            // does not. See Product.BaseUnitsPerLarge — this is the module's one genuine
            // trap, and the message has to name the right unit or the person enters the
            // wrong number.
            validator.RuleFor(x => x.MidPerLarge)
                .NotNull().WithMessage("Enter how many are in one bulk pack.");

            validator.RuleFor(x => x.MidPerLarge)
                .GreaterThan(1).WithMessage("A bulk pack has to hold more than one.")
                .When(x => x.MidPerLarge is not null);

            // Same rule as the pack price above: required only once a base price is given.
            validator.RuleFor(x => x.PricePerLarge)
                .NotNull().WithMessage("Enter the price for the bulk pack.")
                .When(x => x.PricePerBase is not null);

            validator.RuleFor(x => x.PricePerLarge)
                .GreaterThanOrEqualTo(0).WithMessage("A price cannot be negative.")
                .When(x => x.PricePerLarge is not null);
        });

        validator.When(x => string.IsNullOrWhiteSpace(x.LargeUnitName), () =>
        {
            validator.RuleFor(x => x.MidPerLarge)
                .Null().WithMessage("Remove the bulk count, or name the bulk pack.");

            validator.RuleFor(x => x.PricePerLarge)
                .Null().WithMessage("Remove the bulk price, or name the bulk pack.");
        });

        // A mid unit named the same as the base unit makes every quantity ambiguous.
        validator.RuleFor(x => x.MidUnitName)
            .Must((request, mid) =>
                string.IsNullOrWhiteSpace(mid)
                || !string.Equals(mid.Trim(), request.BaseUnitName?.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            .WithMessage("The pack needs a different name from the single unit.");

        validator.RuleFor(x => x.LargeUnitName)
            .Must((request, large) =>
                string.IsNullOrWhiteSpace(large)
                || (!string.Equals(large.Trim(), request.BaseUnitName?.Trim(),
                        StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(large.Trim(), request.MidUnitName?.Trim(),
                        StringComparison.OrdinalIgnoreCase)))
            .WithMessage("The bulk pack needs a different name from the other units.");
    }
}
