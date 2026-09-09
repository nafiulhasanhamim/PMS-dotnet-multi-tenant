using FuzzySharp;

namespace PMS.Application.Common.Products;

/// <summary>
/// Scores how close a search term is to a catalogue entry, 0–100.
///
/// <para><b>Case is folded first, and that is not cosmetic.</b> FuzzySharp compares the raw
/// strings it is given, so "napaa" against "Napa" fails on the capital N: the longest common
/// run drops to "apa" and the score lands at 67 — under the 70 threshold — which made the
/// fuzzy fallback silently useless for every brand name in the catalogue, since they are all
/// capitalised. Lowercasing here rather than relying on a library preprocessing mode keeps
/// the behaviour visible and testable.</para>
///
/// <para>Extracted from the query handler so it can be tested directly. The threshold is the
/// other half of the contract and lives with the handler, because tuning it is a product
/// decision about how much noise is acceptable.</para>
/// </summary>
public static class CatalogFuzzyScorer
{
    /// <summary>
    /// The better of the brand and generic scores.
    ///
    /// <para>Both are tried because people search either way — "napa" is a brand, and
    /// "paracetamol" is what is in it — and a term that matches one strongly should not be
    /// dragged down by the other.</para>
    ///
    /// <para><c>WeightedRatio</c> rather than a plain ratio: it also considers the term as a
    /// substring or a reordering of the target, which covers the realistic mistakes — a
    /// dropped letter, a doubled letter, or typing "napa 500" against "Napa".</para>
    /// </summary>
    public static int Score(string term, string brandName, string? genericName)
    {
        var needle = Normalise(term);

        if (needle.Length == 0)
        {
            return 0;
        }

        var brandScore = ScoreOne(needle, brandName);
        var genericScore = ScoreOne(needle, genericName);

        return Math.Max(brandScore, genericScore);
    }

    private static int ScoreOne(string needle, string? candidate)
    {
        var hay = Normalise(candidate);

        return hay.Length == 0 ? 0 : Fuzz.WeightedRatio(needle, hay);
    }

    /// <summary>
    /// Lower-cased and trimmed, invariant culture.
    ///
    /// Invariant because Turkish "I" folding would otherwise make a search result depend on
    /// the server's locale.
    /// </summary>
    private static string Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
}
