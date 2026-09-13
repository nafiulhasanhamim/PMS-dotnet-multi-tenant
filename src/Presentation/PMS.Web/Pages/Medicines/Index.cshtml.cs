using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Medicines;

/// <summary>
/// The pharmacy's medicines.
///
/// <para>Authorized for any tenant user, including an Employee — looking up what the pharmacy
/// stocks is the job. What an Employee cannot do is change anything, and the API withholds
/// prices from this list for them, so the price column simply has nothing to render.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantUser)]
public class IndexModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public IndexModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "status")]
    public ProductStatusFilter Status { get; set; } = ProductStatusFilter.Active;

    [BindProperty(SupportsGet = true, Name = "abx")]
    public bool AntibioticOnly { get; set; }

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public ApiPage<ProductListItem> Products { get; private set; } = ApiPage<ProductListItem>.Empty;

    /// <summary>
    /// How many active medicines are still unpriced, for the banner.
    ///
    /// <para>Read with a separate one-row query rather than counted from the page in front of
    /// the person: the count has to be the pharmacy's total, and the page they are looking at
    /// is twenty-five rows of a filtered list.</para>
    /// </summary>
    public int IncompleteCount { get; private set; }

    /// <summary>True when any filter is narrowing the list, so an empty result can say so.</summary>
    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Search)
        || AntibioticOnly
        || Status != ProductStatusFilter.Active;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetProductsAsync(
            ProductListType.Medicine, Search, Status, AntibioticOnly,
            productType: null, page: PageNumber, pageSize: 25, ct: ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Products = result.Value ?? ApiPage<ProductListItem>.Empty;

        // A second call, and cheap: the API answers it from a filtered index over the
        // incomplete rows only. Skipped when the list is already filtered to them, because
        // then the banner is not shown and the number would go unused.
        if (Status != ProductStatusFilter.SetupIncomplete)
        {
            var incomplete = await _api.GetProductsAsync(
                ProductListType.Medicine,
                search: null,
                status: ProductStatusFilter.SetupIncomplete,
                page: 1,
                pageSize: 1,
                ct: ct);

            if (incomplete.IsSuccess)
            {
                IncompleteCount = incomplete.Value?.Total ?? 0;
            }
        }

        return Page();
    }

    /// <summary>
    /// Deactivate or reactivate. Admin only — the API enforces it, and the buttons are only
    /// rendered for an Admin, in that order of importance.
    /// </summary>
    public async Task<IActionResult> OnPostActiveAsync(Guid id, bool isActive, CancellationToken ct)
    {
        var result = await _api.SetProductActiveAsync(id, isActive, ct);

        if (!result.IsSuccess)
        {
            var redirect = await HandleFailureAsync(result);
            if (redirect is not null)
            {
                return redirect;
            }

            await OnGetAsync(ct);
            return Page();
        }

        SuccessMessage = isActive
            ? $"{result.Value?.BrandName} is active again."
            : $"{result.Value?.BrandName} was deactivated. Existing stock and records are kept.";

        return RedirectToPage(new { q = Search, status = Status, abx = AntibioticOnly, p = PageNumber });
    }
}
