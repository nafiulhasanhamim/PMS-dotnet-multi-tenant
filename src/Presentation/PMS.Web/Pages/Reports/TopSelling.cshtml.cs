using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Reports;

/// <summary>
/// What moved, net of returns.
///
/// <para>Sortable by quantity, revenue or profit, and the three answer different questions: the
/// product that moves most is rarely the product that earns most, and an owner deciding what to
/// reorder wants the first while an owner deciding what to promote wants the third.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class TopSellingModel : ReportRangePageModel
{
    private readonly PmsApiClient _api;

    public TopSellingModel(PmsApiClient api) => _api = api;

    [BindProperty(SupportsGet = true, Name = "sort")]
    public TopSellingSort SortBy { get; set; } = TopSellingSort.Quantity;

    [BindProperty(SupportsGet = true, Name = "type")]
    public ProductType? ProductType { get; set; }

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public ApiPage<TopSellingProduct> Rows { get; private set; } = ApiPage<TopSellingProduct>.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Prepare();

        var result = await _api.GetTopSellingProductsAsync(
            From, To, SortBy, ProductType, PageNumber, pageSize: 25, ct: ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Rows = result.Value ?? ApiPage<TopSellingProduct>.Empty;

        return Page();
    }

    public Task<IActionResult> OnGetExportAsync(CancellationToken ct) =>
        ExportAsync(
            _api,
            "api/reports/top-selling/export?"
                + PmsApiClient.TopSellingQuery(From, To, SortBy, ProductType),
            "top-selling.csv",
            ct);

    public override Dictionary<string, string> RouteValues => new()
    {
        ["from"] = From?.ToString("yyyy-MM-dd") ?? string.Empty,
        ["to"] = To?.ToString("yyyy-MM-dd") ?? string.Empty,
        ["sort"] = SortBy.ToString(),
        ["type"] = ProductType?.ToString() ?? string.Empty,
    };
}
