using PMS.Domain.Entities;
using PMS.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace PMS.UnitTests.Domain;

/// <summary>
/// <c>Product.IsSetupComplete</c>.
///
/// <para>Stored rather than derived on read, so it can be filtered and counted in SQL — which
/// means it can go stale, and these are the tests that say it does not. Every path that
/// changes a price or a unit level has to recompute it, including the ones where that is not
/// obvious: adding a bulk pack to a priced product makes it incomplete again.</para>
///
/// <para>Module 5 will refuse to sell a product with this false, so a stale true is a product
/// sold at a price nobody set.</para>
/// </summary>
public class ProductSetupCompleteTests
{
    /// <summary>Named so it does not shadow the entity type in reflection tests.</summary>
    private static PMS.Domain.Entities.Product Product(decimal? pricePerBase = 1.20m) =>
        new(ProductType.Medicine, "Napa 500", "piece", pricePerBase);

    // ── The four unit shapes ────────────────────────────────────────────────────────────

    [Fact]
    public void BaseOnly_IsCompleteWithOnePrice()
    {
        var product = Product(1.20m);

        product.IsSetupComplete.Should().BeTrue(
            "a product sold only in pieces needs one price, not three");
    }

    [Fact]
    public void BaseOnly_IsIncompleteWithNoPrice()
        => Product(pricePerBase: null).IsSetupComplete.Should().BeFalse();

    [Fact]
    public void BaseAndMid_NeedsBothPrices()
    {
        var product = Product();
        product.SetUnits("piece", "strip", null, 10, null);

        product.SetPrices(1.20m, null, null);
        product.IsSetupComplete.Should().BeFalse("the strip has no price");

        product.SetPrices(1.20m, 12m, null);
        product.IsSetupComplete.Should().BeTrue();
    }

    [Fact]
    public void BaseAndLarge_NeedsBothPrices()
    {
        var product = Product();
        product.SetUnits("bottle", null, "carton", null, 24);

        product.SetPrices(180m, null, null);
        product.IsSetupComplete.Should().BeFalse("the carton has no price");

        product.SetPrices(180m, null, 4_320m);
        product.IsSetupComplete.Should().BeTrue();
    }

    [Fact]
    public void AllThreeLevels_NeedAllThreePrices()
    {
        var product = Product();
        product.SetUnits("piece", "strip", "box", 10, 10);

        product.SetPrices(1.20m, 12m, null);
        product.IsSetupComplete.Should().BeFalse("the box has no price");

        product.SetPrices(1.20m, 12m, 115m);
        product.IsSetupComplete.Should().BeTrue();
    }

    // ── The non-obvious recomputations ──────────────────────────────────────────────────

    [Fact]
    public void AddingALevelToAPricedProduct_MakesItIncompleteAgain()
    {
        var product = Product(1.20m);
        product.IsSetupComplete.Should().BeTrue();

        // A bulk pack with no bulk price. The product was complete a moment ago and is not
        // any more, which is why the unit setter recomputes and not only the price setter.
        product.SetUnits("piece", null, "box", null, 100);

        product.IsSetupComplete.Should().BeFalse();
    }

    [Fact]
    public void RemovingALevel_CanCompleteAProduct()
    {
        var product = Product();
        product.SetUnits("piece", "strip", null, 10, null);
        product.SetPrices(1.20m, null, null);
        product.IsSetupComplete.Should().BeFalse();

        // Dropping the level drops the requirement — and SetUnits nulls the orphaned price
        // too, so nothing is left behind that nothing can interpret.
        product.SetUnits("piece", null, null, null, null);

        product.IsSetupComplete.Should().BeTrue();
    }

    [Fact]
    public void Update_RecomputesFromTheFinalState()
    {
        var product = Product();

        product.Update(
            ProductType.Medicine, "Napa 500", "Beximco", null, "Paracetamol", "500 mg",
            "Tablet", false, "piece", "strip", "box", 10, 10,
            pricePerBase: 1.20m, pricePerMid: 12m, pricePerLarge: null,
            reorderLevel: 100, shelfLocation: null);

        product.IsSetupComplete.Should().BeFalse(
            "Update sets units then prices, and the recompute has to reflect both");
    }

    // ── Zero, which is the reason the column is nullable rather than defaulted ──────────

    [Fact]
    public void ZeroIsAPrice_NotAnAbsentOne()
    {
        // A sample or a giveaway really does cost nothing. If "unpriced" were stored as zero
        // this product would be indistinguishable from one nobody has priced — the sentinel
        // confusion the nullable column exists to prevent.
        var product = Product(0m);

        product.IsSetupComplete.Should().BeTrue();
        product.PricePerBase.Should().Be(0m);
    }

    [Fact]
    public void NullAndZero_AreDifferentStates()
    {
        Product(0m).IsSetupComplete.Should().BeTrue();
        Product(null).IsSetupComplete.Should().BeFalse();
    }

    // ── Nothing outside the entity writes the flag ──────────────────────────────────────

    [Fact]
    public void TheFlagHasNoPublicSetter()
    {
        var property = typeof(PMS.Domain.Entities.Product)
            .GetProperty(nameof(PMS.Domain.Entities.Product.IsSetupComplete));

        property.Should().NotBeNull();
        property!.SetMethod!.IsPublic.Should().BeFalse(
            "the flag is stored, so it can go stale; the only writer is the entity's own "
            + "recompute, and a public setter would let a caller assert a completeness the "
            + "prices do not support");
    }
}
