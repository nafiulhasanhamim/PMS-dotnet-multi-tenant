using PMS.Application.Common.Products;
using FluentAssertions;
using Xunit;

namespace PMS.UnitTests.Application.Products;

/// <summary>
/// <c>ProductKeys.Identity</c> — what makes two products the same product.
///
/// <para>The database materialises the same expression as a persisted computed column and puts
/// a single unique index over it (migration 010). These tests pin the C# half; if the two ever
/// disagree, the in-memory duplicate check reports a clash the constraint does not, or misses
/// one it does.</para>
/// </summary>
public class ProductKeysTests
{
    // ── Dosage form is part of the identity: the whole point of migration 010 ───────────

    [Fact]
    public void SameBrandAndStrength_DifferentDosageForm_AreDifferentProducts()
    {
        // Nyclobate 0.05% exists in the reference catalogue six times, as a lotion, a spray, a
        // shampoo, a scalp solution, an ointment and a cream. A pharmacy stocks several of
        // them at different prices. Before dosage form joined the identity it could hold one.
        var cream = ProductKeys.Identity("Nyclobate", "0.05%", "Cream");
        var lotion = ProductKeys.Identity("Nyclobate", "0.05%", "Lotion");

        cream.Should().NotBe(lotion);
    }

    [Fact]
    public void SameBrandStrengthAndForm_IsTheSameProduct()
        => ProductKeys.Identity("Napa", "500 mg", "Tablet")
            .Should().Be(ProductKeys.Identity("Napa", "500 mg", "Tablet"));

    [Fact]
    public void DifferentStrength_IsADifferentProduct()
        => ProductKeys.Identity("Napa", "500 mg", "Tablet")
            .Should().NotBe(ProductKeys.Identity("Napa", "665 mg", "Tablet"));

    // ── Normalisation, matching the database collation ──────────────────────────────────

    [Theory]
    [InlineData("Napa", "NAPA")]
    [InlineData("Napa", "napa")]
    [InlineData("Napa", "  Napa  ")]
    public void CaseAndSurroundingSpaceDoNotMatter(string one, string other)
        => ProductKeys.Identity(one, "500 mg", "Tablet")
            .Should().Be(ProductKeys.Identity(other, "500 mg", "Tablet"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AbsentPartsCollapseToTheSameThing(string? empty)
    {
        // ISNULL in the computed column turns a missing part into an empty string. Whitespace
        // has to land in the same place, or a strength of " " and no strength would be two
        // different products in memory and one in the database.
        ProductKeys.Identity("Hartmann Solution", empty, empty)
            .Should().Be(ProductKeys.Identity("Hartmann Solution", null, null));
    }

    // ── The separator earns its keep ────────────────────────────────────────────────────

    [Fact]
    public void PartsCannotRunTogetherIntoTheSameKey()
    {
        // Without a separator these two would both be "napa500mgtablet". A space would not
        // help either — brand names contain spaces.
        var brandInName = ProductKeys.Identity("Napa 500", "mg", "Tablet");
        var brandInStrength = ProductKeys.Identity("Napa", "500 mg", "Tablet");

        brandInName.Should().NotBe(brandInStrength);
    }

    [Fact]
    public void AMissingMiddlePartDoesNotShiftTheOthers()
    {
        // "Napa" with no strength and a Tablet form must not equal "Napa" with a strength of
        // "Tablet" and no form. Positional separators are what prevent that.
        ProductKeys.Identity("Napa", null, "Tablet")
            .Should().NotBe(ProductKeys.Identity("Napa", "Tablet", null));
    }

    // ── Describe: what the conflict messages say ────────────────────────────────────────

    [Fact]
    public void Describe_NamesAllThreeParts()
        => ProductKeys.Describe("Napa 500", "500 mg", "Tablet")
            .Should().Be("'Napa 500' at 500 mg (Tablet)");

    [Fact]
    public void Describe_OmitsThePartsThatAreAbsent()
    {
        ProductKeys.Describe("Hartmann Solution 500ml", null, null)
            .Should().Be("'Hartmann Solution 500ml'");

        ProductKeys.Describe("Allbeevit", null, "Syrup")
            .Should().Be("'Allbeevit' (Syrup)");
    }
}
