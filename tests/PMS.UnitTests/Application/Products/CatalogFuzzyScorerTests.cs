using PMS.Application.Common.Products;
using FluentAssertions;
using Xunit;

namespace PMS.UnitTests.Application.Products;

/// <summary>
/// The scoring behind the forgiving catalogue search.
///
/// <para>These exist because the first implementation was quietly broken. FuzzySharp compares
/// the raw strings it is given, and every brand name in the catalogue is capitalised, so
/// "napaa" against "Napa" scored 67 — under the 70 threshold — and the fuzzy fallback returned
/// unrelated medicines instead of the obvious one. The API still answered 200 with a
/// plausible-looking list, which is why this needs a test rather than a manual check.</para>
///
/// <para>The threshold itself (70) lives in the query handler; these assert the scores it is
/// applied to, on both sides of it.</para>
/// </summary>
public class CatalogFuzzyScorerTests
{
    private const int Threshold = 70;

    [Theory]
    // The acceptance cases: a doubled letter, and a single wrong vowel.
    [InlineData("napaa", "Napa")]
    [InlineData("sergal", "Sergel")]
    [InlineData("ciproccin", "Ciprocin")]
    [InlineData("azithral", "Azithral")]
    // Case alone must never be what decides a match.
    [InlineData("NAPA", "Napa")]
    [InlineData("napa", "NAPA")]
    // Dropped and transposed letters.
    [InlineData("secl", "Seclo")]
    [InlineData("mnoas", "Monas")]
    public void ScoresCloseMisspellingsAboveTheThreshold(string term, string brand)
    {
        var score = CatalogFuzzyScorer.Score(term, brand, genericName: null);

        score.Should().BeGreaterThanOrEqualTo(Threshold,
            "'{0}' is a plausible attempt at '{1}' and has to be suggested", term, brand);
    }

    [Theory]
    [InlineData("xyzabc123", "Napa")]
    [InlineData("zzzzzz", "Sergel")]
    [InlineData("aspirin", "Napa")]
    public void ScoresUnrelatedTermsBelowTheThreshold(string term, string brand)
    {
        // Below the line matters as much as above it. A threshold that lets garbage through
        // returns a page of irrelevant medicines, and someone may import one of them.
        var score = CatalogFuzzyScorer.Score(term, brand, genericName: null);

        score.Should().BeLessThan(Threshold,
            "'{0}' has nothing to do with '{1}'", term, brand);
    }

    [Fact]
    public void MatchesOnTheGenericWhenTheBrandIsUnrelated()
    {
        // Someone searching an ingredient should find brands of it.
        var score = CatalogFuzzyScorer.Score("paracetamol", "Napa", "Paracetamol");

        score.Should().BeGreaterThanOrEqualTo(Threshold);
    }

    [Fact]
    public void TakesTheBetterOfTheTwoFields()
    {
        // A strong brand match must not be dragged down by an unrelated generic.
        var brandOnly = CatalogFuzzyScorer.Score("napaa", "Napa", "Something Unrelated Entirely");
        var brandAlone = CatalogFuzzyScorer.Score("napaa", "Napa", null);

        brandOnly.Should().Be(brandAlone);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ScoresNothingForAnEmptyTerm(string? term) =>
        CatalogFuzzyScorer.Score(term!, "Napa", "Paracetamol").Should().Be(0);

    [Fact]
    public void ScoresNothingWhenThereIsNothingToMatchAgainst() =>
        CatalogFuzzyScorer.Score("napa", string.Empty, null).Should().Be(0);
}
