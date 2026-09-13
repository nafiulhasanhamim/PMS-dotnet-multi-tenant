using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.Selection;

namespace PMS.Web.Pages.Medicines;

/// <summary>
/// Step 1 of importing from the reference catalogue: search the shared 21,714-medicine list.
///
/// <para><b>Two ways on from here.</b> One medicine at a time through
/// <c>/medicines/create?catalogId={id}</c>, which pre-fills a form from the chosen entry; or
/// many at once by ticking rows and continuing to <c>/medicines/import/setup</c>. The single
/// path remains because it is the right one for adding one thing to an established catalogue;
/// the bulk path exists because onboarding means two hundred of them, and doing that one form
/// at a time is the difference between an afternoon and a fortnight.</para>
///
/// <para>Either way it is two steps and not a one-click import: the catalogue does not know
/// this pharmacy's prices or pack sizes, and the antibiotic flag it carries is a
/// machine-derived guess that a pharmacist has to confirm.</para>
///
/// <para>The selection lives in session, so it survives paging <em>and</em> a completely new
/// search — tick three from "napa", search "azin", tick two more, continue with five. See
/// <see cref="CatalogSelection"/> for why not a hidden field, a cookie or the query
/// string.</para>
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

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public CatalogSearchResult? Results { get; private set; }

    /// <summary>What is ticked, across every search made in this session.</summary>
    public CatalogSelection Selection { get; private set; } = new();

    /// <summary>
    /// True when every selectable row on this page is already ticked, so the header checkbox
    /// can show the right state.
    /// </summary>
    public bool AllOnPageSelected =>
        SelectableOnPage.Count > 0
        && SelectableOnPage.All(item => Selection.Contains(item.Id));

    /// <summary>
    /// The rows a person could tick: already-imported entries are shown but not selectable,
    /// because importing them again is exactly what the flag is warning about.
    /// </summary>
    public IReadOnlyList<CatalogMedicineSearchItem> SelectableOnPage =>
        Results?.Items.Where(item => !item.AlreadyImported).ToList() ?? [];

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
        Selection = HttpContext.Session.GetCatalogSelection();

        if (!HasSearched)
        {
            return Page();
        }

        var result = await _api.SearchCatalogAsync(
            Term!.Trim(), page: PageNumber < 1 ? 1 : PageNumber, pageSize: 20, ct: ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Results = result.Value;

        return Page();
    }

    /// <summary>Ticks or unticks one row, then returns to exactly the same search and page.</summary>
    public IActionResult OnPostToggle(int catalogMedicineId, string brandName, bool selected)
    {
        var selection = HttpContext.Session.GetCatalogSelection();

        if (selected)
        {
            if (!selection.Add(catalogMedicineId, brandName))
            {
                // At the cap. Said here rather than after somebody has filled in a grid of
                // 250 rows and had the import refused.
                SelectionMessage =
                    $"You can import {CatalogSelection.MaxItems} medicines at a time. "
                    + "Continue with these, then start another selection.";
            }
        }
        else
        {
            selection.Remove(catalogMedicineId);
        }

        HttpContext.Session.SetCatalogSelection(selection);

        return RedirectToSearch();
    }

    /// <summary>Ticks every selectable row on this page, or unticks them all.</summary>
    public async Task<IActionResult> OnPostToggleAllAsync(bool selected, CancellationToken ct)
    {
        Selection = HttpContext.Session.GetCatalogSelection();

        // The page has to be re-fetched: the form posts which page it was on, not what was on
        // it, and "select all on this page" needs to know what that was.
        if (HasSearched)
        {
            var result = await _api.SearchCatalogAsync(
                Term!.Trim(), page: PageNumber < 1 ? 1 : PageNumber, pageSize: 20, ct: ct);

            if (result.IsSuccess)
            {
                Results = result.Value;
            }
        }

        var selection = HttpContext.Session.GetCatalogSelection();
        var hitCap = false;

        foreach (var item in SelectableOnPage)
        {
            if (selected)
            {
                if (!selection.Add(item.Id, item.BrandName))
                {
                    hitCap = true;
                    break;
                }
            }
            else
            {
                selection.Remove(item.Id);
            }
        }

        HttpContext.Session.SetCatalogSelection(selection);

        if (hitCap)
        {
            SelectionMessage =
                $"Only the first {CatalogSelection.MaxItems} were selected — that is the most "
                + "one import can carry.";
        }

        return RedirectToSearch();
    }

    public IActionResult OnPostClearSelection()
    {
        HttpContext.Session.SetCatalogSelection(new CatalogSelection());

        return RedirectToSearch();
    }

    /// <summary>A notice about the selection itself, carried across the redirect.</summary>
    [TempData]
    public string? SelectionMessage { get; set; }

    /// <summary>
    /// Back to the same search and page.
    ///
    /// <para>Every selection action is a POST followed by a redirect, so a tick survives a
    /// refresh and the back button behaves. Returning the page directly would leave a POST in
    /// the history, and re-submitting it would toggle the row a second time.</para>
    /// </summary>
    private IActionResult RedirectToSearch() =>
        RedirectToPage(new { q = Term, p = PageNumber });
}
