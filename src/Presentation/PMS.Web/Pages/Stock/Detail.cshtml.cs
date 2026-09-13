using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Stock;

/// <summary>
/// One product's stock, batch by batch.
///
/// <para>This is where the list stops aggregating and starts naming physical packs, because
/// this is the point at which the difference matters: which one to sell next, which one is
/// about to expire, and what each of them cost.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantUser)]
public class DetailModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public DetailModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true, Name = "productId")]
    public Guid ProductId { get; set; }

    /// <summary>Which page of depleted batches. Live batches are never paginated.</summary>
    [BindProperty(SupportsGet = true, Name = "dp")]
    public int DepletedPage { get; set; } = 1;

    public ProductStockModel? Stock { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetProductStockAsync(
            ProductId, DepletedPage, depletedPageSize: 10, ct: ct);

        if (!result.IsSuccess)
        {
            // A 404 here is another pharmacy's product id, or one that never existed. Both
            // are the same thing from inside this pharmacy, and both belong on the list page
            // with an explanation rather than on a broken detail page.
            if (result.Problem?.Status == StatusCodes.Status404NotFound)
            {
                SuccessMessage = null;
                return RedirectToPage("/Stock/Index");
            }

            return await HandleFailureAsync(result) ?? Page();
        }

        Stock = result.Value;

        return Page();
    }

    /// <summary>
    /// Total pages of depleted batches, worked out here rather than in the view.
    ///
    /// <para>The depleted list is paginated and the live one is not, which looks inconsistent
    /// and is not: a product holds a handful of live batches at a time and accumulates
    /// depleted ones for as long as the pharmacy trades.</para>
    /// </summary>
    public int DepletedTotalPages =>
        Stock is null || Stock.DepletedBatchCount == 0
            ? 1
            : (int)Math.Ceiling(Stock.DepletedBatchCount / 10.0);
}
