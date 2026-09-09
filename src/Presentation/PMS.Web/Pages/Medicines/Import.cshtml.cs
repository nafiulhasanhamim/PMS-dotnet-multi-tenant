using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Medicines;

/// <summary>
/// Step 1 of importing from the reference catalogue: search the shared 21,714-medicine list.
///
/// <para>Step 2 is <c>/medicines/create?catalogId={id}</c>, which pre-fills a form from the
/// chosen entry. Two steps rather than a one-click import because the catalogue does not know
/// this pharmacy's prices or pack sizes, and because the antibiotic flag it carries is a
/// machine-derived guess that a pharmacist has to confirm.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantWriter)]
public class ImportModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public ImportModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Term { get; set; }

    public CatalogSearchResult? Results { get; private set; }

    /// <summary>
    /// True once a search has actually run, so the page can tell "nothing typed yet" from
    /// "nothing found".
    /// </summary>
    public bool HasSearched => !string.IsNullOrWhiteSpace(Term) && Term.Trim().Length >= 2;

    /// <summary>
    /// True when the results are approximate. The heading changes on this, because presenting
    /// a fuzzy guess as a match is how somebody imports the wrong medicine.
    /// </summary>
    public bool IsSuggestion => Results?.MatchType == CatalogMatchType.Suggestion;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!HasSearched)
        {
            return Page();
        }

        var result = await _api.SearchCatalogAsync(Term!.Trim(), take: 20, ct: ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Results = result.Value;

        return Page();
    }
}
