using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Stock;

/// <summary>
/// What is on the shelves, a row per product.
///
/// <para>Authorized for any tenant user. An Employee needs to answer "have we got any Napa?"
/// without being able to change the answer, and this list carries no prices of any kind — so
/// unlike the product list there is nothing here to withhold by role.</para>
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

    [BindProperty(SupportsGet = true, Name = "stock")]
    public StockStatusFilter StockStatus { get; set; } = StockStatusFilter.All;

    [BindProperty(SupportsGet = true, Name = "expiry")]
    public ExpiryStatusFilter ExpiryStatus { get; set; } = ExpiryStatusFilter.All;

    [BindProperty(SupportsGet = true, Name = "type")]
    public StockProductTypeFilter ProductType { get; set; } = StockProductTypeFilter.All;

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public ApiPage<StockListItem> Stock { get; private set; } = ApiPage<StockListItem>.Empty;

    /// <summary>True when a filter is narrowing the list, so an empty result can say which.</summary>
    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Search)
        || StockStatus != StockStatusFilter.All
        || ExpiryStatus != ExpiryStatusFilter.All
        || ProductType != StockProductTypeFilter.All;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetStockAsync(
            Search, StockStatus, ExpiryStatus, ProductType, PageNumber, pageSize: 25, ct: ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Stock = result.Value ?? ApiPage<StockListItem>.Empty;

        return Page();
    }

    /// <summary>The filter values, for the pagination links to carry.</summary>
    public Dictionary<string, string> RouteValues => new()
    {
        ["q"] = Search ?? string.Empty,
        ["stock"] = StockStatus.ToString(),
        ["expiry"] = ExpiryStatus.ToString(),
        ["type"] = ProductType.ToString(),
    };
}
