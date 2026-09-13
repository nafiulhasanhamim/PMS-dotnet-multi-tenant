using PMS.Application.Common.DTOs;
using PMS.Application.Common.Products;
using PMS.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Catalog.Queries.SearchCatalogMedicines;

/// <summary>
/// Two-stage catalogue search.
///
/// <para><b>Stage 1 — direct match.</b> An indexed lookup on brand name, then generic name.
/// Prefix first (<c>LIKE 'term%'</c>) so SQL Server can seek the index; contains only as a
/// fallback, because a leading wildcard forces a scan of all 21,714 rows. If this finds
/// enough, the answer is <see cref="CatalogMatchType.Exact"/> and stage 2 never runs — which
/// is the common case and has to stay fast.</para>
///
/// <para><b>Stage 2 — fuzzy fallback.</b> Only when stage 1 finds almost nothing. Brand names
/// like Ciprocin, Azithral and Sergel are easy to mistype, and a strict search that returns
/// nothing on a typo pushes people into entering the medicine by hand — which is exactly the
/// data-quality problem the shared catalogue is meant to solve.</para>
///
/// <para>The candidate set is <b>bounded in the database</b>, never loaded whole. Fuzzy
/// scoring is O(candidates) string work on the app server, so pulling 21,714 rows on every
/// typo would be a memory spike and a CPU burn on a machine serving every pharmacy at
/// once.</para>
/// </summary>
public sealed class SearchCatalogMedicinesQueryHandler
    : IRequestHandler<SearchCatalogMedicinesQuery, CatalogSearchResultDto>
{
    /// <summary>
    /// Below this many direct hits, try fuzzy as well.
    ///
    /// Not zero: one weak hit on a two-letter prefix is worse than one hit plus a few close
    /// suggestions, and a person who mistyped usually gets a hit for something unrelated.
    /// </summary>
    private const int DirectResultsConsideredEnough = 3;

    /// <summary>
    /// Minimum fuzzy score to show a suggestion. 70 is the starting point recommended for
    /// this data and wants tuning against real searches.
    ///
    /// <para>The number does real work in both directions. Too low and "xyzabc123" returns a
    /// page of unrelated medicines, which is worse than nothing because a person may import
    /// one. Too high and "napaa" returns nothing, which is the problem stage 2 exists to
    /// fix. Verified at 70: "napaa" finds Napa, "sergal" finds Sergel, "xyzabc123" finds
    /// nothing.</para>
    /// </summary>
    private const int SimilarityThreshold = 70;

    /// <summary>
    /// Ceiling on rows pulled for scoring. Roughly 4% of the catalogue, and the reason
    /// stage 2 costs milliseconds rather than seconds.
    /// </summary>
    private const int MaxFuzzyCandidates = 800;

    /// <summary>
    /// The most matches gathered before paging, across both stages.
    ///
    /// <para>Two hundred, which is also the import cap — so a person can page through
    /// everything a single import could carry and no further. Higher would mean holding more
    /// rows per request to serve pages almost nobody visits; lower would mean a common search
    /// like "cef" running out of pages before it ran out of relevance.</para>
    /// </summary>
    private const int SearchCeiling = 200;

    private readonly ICatalogSearchQueries _catalog;
    private readonly ILogger<SearchCatalogMedicinesQueryHandler> _logger;

    public SearchCatalogMedicinesQueryHandler(
        ICatalogSearchQueries catalog,
        ILogger<SearchCatalogMedicinesQueryHandler> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public async Task<CatalogSearchResultDto> Handle(
        SearchCatalogMedicinesQuery request, CancellationToken cancellationToken)
    {
        var term = (request.Term ?? string.Empty).Trim();
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 50 ? 20 : request.PageSize;

        if (term.Length < 2)
        {
            // One character matches thousands of things and helps nobody.
            return Empty(term, page, pageSize);
        }

        // Gathered to the ceiling rather than to the page size, because stage 2 scores in
        // memory and cannot be offset — see the query. One page of twenty is then a slice.
        var direct = await _catalog.DirectMatchAsync(term, SearchCeiling, cancellationToken);

        if (direct.Count >= DirectResultsConsideredEnough)
        {
            // Logged at Debug: this is the common, healthy path and would otherwise be the
            // noisiest line in the file. The file sink starts at Information, so it stays out
            // unless somebody lowers the level to investigate.
            _logger.LogDebug(
                "Catalog search '{Term}' matched directly with {ResultCount} results; "
                + "fuzzy stage skipped",
                term,
                direct.Count);

            return Paged(CatalogMatchType.Exact, term, direct, page, pageSize);
        }

        var suggestions = await FindSuggestionsAsync(term, cancellationToken);

        // Anything found directly is a better answer than any guess, so direct hits stay and
        // keep their place; suggestions only fill the space below them.
        var directIds = direct.Select(d => d.Id).ToHashSet();
        var combined = direct
            .Concat(suggestions.Where(s => !directIds.Contains(s.Id)))
            .Take(SearchCeiling)
            .ToList();

        if (combined.Count == 0)
        {
            // Information, not Debug, and this is the line worth having. A search that found
            // nothing is either a medicine genuinely missing from the catalogue - meaning
            // someone is about to type it in by hand - or a threshold set too high. Both are
            // things to know, and neither is visible from anywhere else.
            _logger.LogInformation(
                "Catalog search '{Term}' found nothing: {DirectCount} direct, "
                + "{SuggestionCount} above the similarity threshold of {Threshold}",
                term,
                direct.Count,
                suggestions.Count,
                SimilarityThreshold);

            return Empty(term, page, pageSize);
        }

        // Labelled a suggestion whenever a guess contributed, so the UI can say "did you
        // mean" rather than presenting approximate results as matches.
        var matchType = direct.Count > 0 && suggestions.Count == 0
            ? CatalogMatchType.Exact
            : CatalogMatchType.Suggestion;

        if (matchType == CatalogMatchType.Suggestion)
        {
            // Every fuzzy fallback, with the top score. This is the record to read when
            // tuning SimilarityThreshold against real searches rather than guesses - it says
            // what people actually mistype and how close the best guess came.
            _logger.LogInformation(
                "Catalog search '{Term}' fell back to suggestions: {ResultCount} results, "
                + "top score {TopScore}, threshold {Threshold}",
                term,
                combined.Count,
                combined.Max(c => c.Score),
                SimilarityThreshold);
        }

        return Paged(matchType, term, combined, page, pageSize);
    }

    private static CatalogSearchResultDto Empty(string term, int page, int pageSize) =>
        new(CatalogMatchType.Exact, term, [], Total: 0, page, pageSize, Capped: false);

    /// <summary>
    /// Slices the gathered matches into one page.
    ///
    /// <para>A page beyond the end returns no items rather than an error: a stale link to
    /// page 7 of a search that now finds two things is a mildly confusing empty list, not
    /// something worth a failure.</para>
    /// </summary>
    private static CatalogSearchResultDto Paged(
        CatalogMatchType matchType,
        string term,
        IReadOnlyList<CatalogMedicineSearchItemDto> all,
        int page,
        int pageSize) =>
        new(
            matchType,
            term,
            all.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            all.Count,
            page,
            pageSize,
            Capped: all.Count >= SearchCeiling);

    private async Task<List<CatalogMedicineSearchItemDto>> FindSuggestionsAsync(
        string term, CancellationToken cancellationToken)
    {
        var candidates = await _catalog.FuzzyCandidatesAsync(
            term, MaxFuzzyCandidates, cancellationToken);

        if (candidates.Count == 0)
        {
            return [];
        }

        return candidates
            .Select(candidate => candidate with
            {
                Score = CatalogFuzzyScorer.Score(term, candidate.BrandName, candidate.GenericName),
            })
            .Where(candidate => candidate.Score >= SimilarityThreshold)
            .OrderByDescending(candidate => candidate.Score)
            // A tie between an already-imported entry and a new one should show the new one
            // first: the person is trying to add something.
            .ThenBy(candidate => candidate.AlreadyImported)
            .ThenBy(candidate => candidate.BrandName, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

}
