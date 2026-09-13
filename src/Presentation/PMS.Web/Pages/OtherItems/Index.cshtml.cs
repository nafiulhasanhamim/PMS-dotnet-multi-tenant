using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.OtherItems;

/// <summary>
/// Everything the pharmacy sells that is not a medicine — saline, syringes, diapers, formula,
/// handwash, supplements.
///
/// <para>The same <c>Product</c> table as the medicines list, read through the opposite type
/// filter. Separate screens because the useful columns differ completely: generic name,
/// strength, dosage form and the antibiotic flag are all empty here, and rendering four dead
/// columns on every row is noise. This one gains a product-type filter instead.</para>
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

    /// <summary>Narrows to one non-medicine type. Null means all of them.</summary>
    [BindProperty(SupportsGet = true, Name = "kind")]
    public ProductType? Kind { get; set; }

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public ApiPage<ProductListItem> Products { get; private set; } = ApiPage<ProductListItem>.Empty;

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Search)
        || Kind is not null
        || Status != ProductStatusFilter.Active;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetProductsAsync(
            ProductListType.Other, Search, Status, antibioticOnly: false,
            productType: Kind, page: PageNumber, pageSize: 25, ct: ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Products = result.Value ?? ApiPage<ProductListItem>.Empty;

        return Page();
    }

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

        return RedirectToPage(new { q = Search, status = Status, kind = Kind, p = PageNumber });
    }
}
