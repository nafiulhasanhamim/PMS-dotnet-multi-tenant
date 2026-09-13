using PMS.Application.Features.Products.Commands.CreateProduct;
using PMS.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace PMS.UnitTests.Application.Products;

/// <summary>
/// The type-conditional and unit rules, which are the two things standing between this
/// catalogue and diapers with a strength.
///
/// <para>These exist because one of them shipped broken. In FluentValidation a trailing
/// <c>.When()</c> applies to every rule in the chain, so
/// <c>RuleFor(x => x.BasePerMid).NotNull().GreaterThan(1).When(x => x.BasePerMid is not null)</c>
/// skipped the <c>NotNull</c> exactly when the value was null — and a pack name with no count
/// was accepted with a 201. The API looked fine; only an acceptance probe caught it. Hence a
/// test per rule rather than per feature.</para>
/// </summary>
public class ProductWriteRulesTests
{
    private readonly CreateProductCommandValidator _validator = new();

    private static CreateProductCommand Valid(
        ProductType type = ProductType.Medicine,
        string brand = "Napa 500",
        string? generic = "Paracetamol",
        string? strength = "500 mg",
        string? dosageForm = "Tablet",
        bool isAntibiotic = false,
        string baseUnit = "piece",
        string? midUnit = null,
        string? largeUnit = null,
        int? basePerMid = null,
        int? midPerLarge = null,
        decimal pricePerBase = 1.20m,
        decimal? pricePerMid = null,
        decimal? pricePerLarge = null,
        int reorderLevel = 100) =>
        new(type, brand, "Beximco", "Painkiller", generic, strength, dosageForm, isAntibiotic,
            baseUnit, midUnit, largeUnit, basePerMid, midPerLarge,
            pricePerBase, pricePerMid, pricePerLarge, reorderLevel, "A-1", null);

    private IReadOnlyDictionary<string, string> ErrorsFor(CreateProductCommand command) =>
        _validator.Validate(command).Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.First().ErrorMessage);

    // ── The four valid unit shapes ──────────────────────────────────────────────────────

    [Fact]
    public void AcceptsBaseOnly() =>
        _validator.Validate(Valid(baseUnit: "bag")).IsValid.Should().BeTrue();

    [Fact]
    public void AcceptsBaseAndMid() =>
        _validator.Validate(Valid(midUnit: "strip", basePerMid: 10, pricePerMid: 12m))
            .IsValid.Should().BeTrue();

    [Fact]
    public void AcceptsBaseAndLargeWithNoMid() =>
        // 24 bottles a carton: MidPerLarge counts BASE units here.
        _validator.Validate(Valid(
                type: ProductType.PersonalCare, brand: "Savlon Handwash", generic: null,
                strength: null, dosageForm: null, baseUnit: "bottle",
                largeUnit: "carton", midPerLarge: 24, pricePerBase: 180m, pricePerLarge: 4320m))
            .IsValid.Should().BeTrue();

    [Fact]
    public void AcceptsAllThreeLevels() =>
        _validator.Validate(Valid(
                midUnit: "strip", largeUnit: "box", basePerMid: 10, midPerLarge: 10,
                pricePerMid: 12m, pricePerLarge: 120m))
            .IsValid.Should().BeTrue();

    // ── The regression: a level named without its count ─────────────────────────────────

    [Fact]
    public void RejectsAMidUnitWithNoCount()
    {
        var errors = ErrorsFor(Valid(midUnit: "strip", pricePerMid: 12m));

        errors.Should().ContainKey("BasePerMid")
            .WhoseValue.Should().Contain("how many");
    }

    [Fact]
    public void RejectsALargeUnitWithNoCount()
    {
        var errors = ErrorsFor(Valid(largeUnit: "box", pricePerLarge: 120m));

        errors.Should().ContainKey("MidPerLarge")
            .WhoseValue.Should().Contain("how many");
    }

    [Fact]
    public void RejectsAMidUnitWithNoPrice() =>
        ErrorsFor(Valid(midUnit: "strip", basePerMid: 10))
            .Should().ContainKey("PricePerMid");

    [Fact]
    public void RejectsALargeUnitWithNoPrice() =>
        ErrorsFor(Valid(largeUnit: "box", midPerLarge: 10))
            .Should().ContainKey("PricePerLarge");

    // ── A count named without its level ─────────────────────────────────────────────────

    [Fact]
    public void RejectsACountWithNoMidUnit() =>
        ErrorsFor(Valid(basePerMid: 10)).Should().ContainKey("BasePerMid");

    [Fact]
    public void RejectsACountWithNoLargeUnit() =>
        ErrorsFor(Valid(midPerLarge: 10)).Should().ContainKey("MidPerLarge");

    [Fact]
    public void RejectsAPackOfOne() =>
        // A "pack" holding one unit is just the unit under another name, and it makes every
        // quantity for that product ambiguous.
        ErrorsFor(Valid(midUnit: "strip", basePerMid: 1, pricePerMid: 2m))
            .Should().ContainKey("BasePerMid");

    [Fact]
    public void RejectsAPackNamedTheSameAsTheUnit() =>
        ErrorsFor(Valid(midUnit: "piece", basePerMid: 10, pricePerMid: 12m))
            .Should().ContainKey("MidUnitName");

    // ── Type-conditional rules ──────────────────────────────────────────────────────────

    [Fact]
    public void RejectsAMedicineWithNoGenericName() =>
        ErrorsFor(Valid(generic: null)).Should().ContainKey("GenericName")
            .WhoseValue.Should().Be("A medicine needs a generic name.");

    [Fact]
    public void RejectsAMedicineWithNoDosageForm() =>
        ErrorsFor(Valid(dosageForm: null)).Should().ContainKey("DosageForm");

    [Theory]
    [InlineData(ProductType.MedicalSupply)]
    [InlineData(ProductType.BabyCare)]
    [InlineData(ProductType.PersonalCare)]
    [InlineData(ProductType.Supplement)]
    [InlineData(ProductType.Other)]
    public void RejectsANonMedicineCarryingMedicineFields(ProductType type)
    {
        // The corruption the module exists to prevent: a diaper with a generic name and a
        // strength. Rejected rather than quietly nulled, so a caller learns it was wrong.
        var errors = ErrorsFor(Valid(
            type: type, brand: "Not A Medicine", baseUnit: "pack",
            generic: "Paracetamol", strength: "500 mg", dosageForm: "Tablet",
            isAntibiotic: true));

        errors.Should().ContainKeys("GenericName", "Strength", "DosageForm", "IsAntibiotic");
        errors["IsAntibiotic"].Should().Be("Only a medicine can be marked as an antibiotic.");
    }

    [Fact]
    public void AcceptsANonMedicineWithNoMedicineFields() =>
        _validator.Validate(Valid(
                type: ProductType.BabyCare, brand: "Nan 1 Infant Formula 400g",
                generic: null, strength: null, dosageForm: null, baseUnit: "tin",
                largeUnit: "carton", midPerLarge: 12, pricePerBase: 1450m, pricePerLarge: 17400m))
            .IsValid.Should().BeTrue();

    // ── Basics ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RejectsAnEmptyName() =>
        ErrorsFor(Valid(brand: "  ")).Should().ContainKey("BrandName");

    [Fact]
    public void RejectsAnEmptyBaseUnit() =>
        ErrorsFor(Valid(baseUnit: "  ")).Should().ContainKey("BaseUnitName");

    [Fact]
    public void RejectsANegativePrice() =>
        ErrorsFor(Valid(pricePerBase: -1m)).Should().ContainKey("PricePerBase");

    [Fact]
    public void RejectsANegativeReorderLevel() =>
        ErrorsFor(Valid(reorderLevel: -1)).Should().ContainKey("ReorderLevel");
}
