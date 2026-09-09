using PMS.Application.Common.Units;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace PMS.UnitTests.Application.Units;

/// <summary>
/// The arithmetic Batches, Billing and Reports will all lean on.
///
/// The case that earns most of these tests is <b>base + large with no middle level</b>: there,
/// <c>MidPerLarge</c> counts base units (24 bottles per carton), not middle packs. Read as a
/// middle count and multiplied by anything, a carton of 24 silently becomes a carton of 240.
/// </summary>
public class UnitConversionTests
{
    // ── Fixtures, one per valid unit shape ──────────────────────────────────────────────

    /// <summary>Standard medicine: 10 pieces a strip, 10 strips a box → 100 a box.</summary>
    private static Product ThreeLevel()
    {
        var product = new Product(ProductType.Medicine, "Napa", "piece", 1.20m);
        product.SetUnits("piece", "strip", "box", 10, 10);
        product.SetPrices(1.20m, 12.00m, 120.00m);
        return product;
    }

    /// <summary>Base + mid only: 3 tablets a strip, no box.</summary>
    private static Product BaseAndMid()
    {
        var product = new Product(ProductType.Medicine, "Monas", "piece", 16.00m);
        product.SetUnits("piece", "strip", null, 3, null);
        product.SetPrices(16.00m, 48.00m, null);
        return product;
    }

    /// <summary>
    /// Base + large, no mid. The tricky one: 24 BOTTLES a carton, and MidPerLarge is where
    /// that 24 lives.
    /// </summary>
    private static Product BaseAndLarge()
    {
        var product = new Product(ProductType.PersonalCare, "Savlon Handwash 500ml", "bottle", 180m);
        product.SetUnits("bottle", null, "carton", null, 24);
        product.SetPrices(180m, null, 4_320m);
        return product;
    }

    /// <summary>Base only: a saline bag, sold one at a time and nothing else.</summary>
    private static Product BaseOnly()
    {
        var product = new Product(ProductType.MedicalSupply, "Normal Saline 1000ml", "bag", 95m);
        product.SetUnits("bag", null, null, null, null);
        product.SetPrices(95m, null, null);
        return product;
    }

    // ── 1. Three-level ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, UnitLevel.Base, 1)]
    [InlineData(7, UnitLevel.Base, 7)]
    [InlineData(1, UnitLevel.Mid, 10)]
    [InlineData(3, UnitLevel.Mid, 30)]
    [InlineData(1, UnitLevel.Large, 100)]
    [InlineData(2, UnitLevel.Large, 200)]
    public void ThreeLevel_ConvertsToBaseUnits(decimal quantity, UnitLevel unit, int expected) =>
        UnitConversion.ToBaseUnits(quantity, unit, ThreeLevel()).Should().Be(expected);

    [Theory]
    [InlineData(1, "1 piece")]
    [InlineData(3, "3 pieces")]
    [InlineData(10, "1 strip")]
    [InlineData(23, "2 strips + 3 pieces")]
    [InlineData(100, "1 box")]
    [InlineData(123, "1 box + 2 strips + 3 pieces")]
    [InlineData(207, "2 boxes + 7 pieces")]      // the empty middle level is omitted
    [InlineData(0, "0 pieces")]
    public void ThreeLevel_RendersReadably(int baseUnits, string expected) =>
        UnitConversion.FromBaseUnits(baseUnits, ThreeLevel()).Should().Be(expected);

    [Fact]
    public void ThreeLevel_DescribesItsPacking() =>
        UnitConversion.DescribePacking(ThreeLevel()).Should().Be("1 box = 10 strips = 100 pieces");

    // ── 2. Base + mid ───────────────────────────────────────────────────────────────────

    [Fact]
    public void BaseAndMid_ConvertsAndRenders()
    {
        var product = BaseAndMid();

        UnitConversion.ToBaseUnits(4, UnitLevel.Mid, product).Should().Be(12);
        UnitConversion.FromBaseUnits(39, product).Should().Be("13 strips");
        UnitConversion.FromBaseUnits(40, product).Should().Be("13 strips + 1 piece");
        UnitConversion.DescribePacking(product).Should().Be("1 strip = 3 pieces");
    }

    [Fact]
    public void BaseAndMid_RejectsALargeQuantity()
    {
        var act = () => UnitConversion.ToBaseUnits(1, UnitLevel.Large, BaseAndMid());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no bulk unit*")
            .WithMessage("*piece*strip*");
    }

    // ── 3. Base + large, no mid — the factor-of-BasePerMid trap ─────────────────────────

    [Fact]
    public void BaseAndLarge_TreatsMidPerLargeAsBaseUnits()
    {
        var product = BaseAndLarge();

        // 24, not 24 × anything. This single assertion is the reason BaseUnitsPerLarge exists.
        product.BaseUnitsPerLarge.Should().Be(24);
        UnitConversion.BaseUnitsIn(UnitLevel.Large, product).Should().Be(24);
        UnitConversion.ToBaseUnits(1, UnitLevel.Large, product).Should().Be(24);
        UnitConversion.ToBaseUnits(3, UnitLevel.Large, product).Should().Be(72);
    }

    [Theory]
    [InlineData(1, "1 bottle")]
    [InlineData(24, "1 carton")]
    [InlineData(29, "1 carton + 5 bottles")]
    [InlineData(53, "2 cartons + 5 bottles")]
    public void BaseAndLarge_RendersReadably(int baseUnits, string expected) =>
        UnitConversion.FromBaseUnits(baseUnits, BaseAndLarge()).Should().Be(expected);

    [Fact]
    public void BaseAndLarge_DescribesItsPacking() =>
        UnitConversion.DescribePacking(BaseAndLarge()).Should().Be("1 carton = 24 bottles");

    [Fact]
    public void BaseAndLarge_PricesPerBaseUnitFromTheCartonPrice() =>
        UnitConversion.PricePerBaseUnit(4_320m, UnitLevel.Large, BaseAndLarge()).Should().Be(180m);

    [Fact]
    public void BaseAndLarge_RejectsAMidQuantity()
    {
        var act = () => UnitConversion.ToBaseUnits(1, UnitLevel.Mid, BaseAndLarge());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no middle unit*")
            .WithMessage("*bottle*carton*");
    }

    // ── 4. Base only ────────────────────────────────────────────────────────────────────

    [Fact]
    public void BaseOnly_ConvertsAndRenders()
    {
        var product = BaseOnly();

        UnitConversion.ToBaseUnits(30, UnitLevel.Base, product).Should().Be(30);
        UnitConversion.FromBaseUnits(30, product).Should().Be("30 bags");
        UnitConversion.FromBaseUnits(1, product).Should().Be("1 bag");
        UnitConversion.DescribePacking(product).Should().Be("Sold as individual bags only");
        UnitConversion.PricePerBaseUnit(95m, UnitLevel.Base, product).Should().Be(95m);
    }

    // ── 5. Rejecting a level the product does not define ────────────────────────────────

    [Theory]
    [InlineData(UnitLevel.Mid)]
    [InlineData(UnitLevel.Large)]
    public void BaseOnly_RejectsEveryOtherLevel(UnitLevel unit)
    {
        // Throws rather than returning 0: a UI must only offer levels the product has, so
        // reaching here is a bug, and returning 0 would record a sale of nothing.
        var act = () => UnitConversion.ToBaseUnits(1, unit, BaseOnly());

        act.Should().Throw<InvalidOperationException>().WithMessage("*Normal Saline*");
    }

    // ── Guards ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RejectsAQuantityThatIsNotWholeBaseUnits()
    {
        // Half a piece is not 0 and not 1. Rounding either way loses or invents stock.
        var act = () => UnitConversion.ToBaseUnits(0.5m, UnitLevel.Base, ThreeLevel());

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*whole*");
    }

    [Fact]
    public void AcceptsAFractionalPackThatLandsOnWholeBaseUnits() =>
        // Half a box of 100 is 50 pieces, which is a real quantity.
        UnitConversion.ToBaseUnits(0.5m, UnitLevel.Large, ThreeLevel()).Should().Be(50);

    [Fact]
    public void RejectsANegativeQuantity()
    {
        var act = () => UnitConversion.ToBaseUnits(-1, UnitLevel.Base, ThreeLevel());

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void PricePerBaseUnit_DoesNotRound()
    {
        var product = BaseAndMid();   // 3 pieces a strip

        // 10 / 3 exactly, not 3.33. Rounding here would lose money on every line.
        UnitConversion.PricePerBaseUnit(10m, UnitLevel.Mid, product)
            .Should().Be(10m / 3m);
    }
}
