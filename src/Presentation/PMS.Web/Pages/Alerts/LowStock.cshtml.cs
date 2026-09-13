using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Alerts;

/// <summary>
/// Products at or below their reorder level.
///
/// <para>The total behind each row is <em>sellable</em> stock: expired batches are excluded by
/// the API. That is the whole reason a product can appear here with a visibly full shelf, and
/// the list says so on any row where it happens.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantUser)]
public class LowStockModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public LowStockModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true, Name = "type")]
    public ProductType? ProductType { get; set; }

    [BindProperty(SupportsGet = true, Name = "status")]
    public LowStockStatusFilter Status { get; set; } = LowStockStatusFilter.All;

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public ApiPage<LowStockProduct> Products { get; private set; } =
        ApiPage<LowStockProduct>.Empty;

    public bool HasFilters =>
        ProductType is not null || Status != LowStockStatusFilter.All;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetLowStockAsync(
            ProductType, Status, PageNumber, pageSize: 25, ct: ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Products = result.Value ?? ApiPage<LowStockProduct>.Empty;

        return Page();
    }

    public Dictionary<string, string> RouteValues => new()
    {
        ["type"] = ProductType?.ToString() ?? string.Empty,
        ["status"] = Status.ToString(),
    };
}
