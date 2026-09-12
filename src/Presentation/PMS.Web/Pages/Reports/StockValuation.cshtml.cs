using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Reports;

/// <summary>
/// What the shelves are worth at cost.
///
/// <para>A snapshot with no date range, because there is no history of stock levels to look back
/// through — "as at last month" is a question this system cannot answer, and offering a date
/// picker that silently ignored it would be worse than not offering one.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class StockValuationModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public StockValuationModel(PmsApiClient api) => _api = api;

    [BindProperty(SupportsGet = true, Name = "type")]
    public ProductType? ProductType { get; set; }

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public StockValuationPage Report { get; private set; } = StockValuationPage.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetStockValuationAsync(
            ProductType, PageNumber, pageSize: 25, ct: ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Report = result.Value ?? StockValuationPage.Empty;

        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync(CancellationToken ct)
    {
        var path = "api/reports/stock-valuation/export"
            + (ProductType is { } t ? $"?productType={(int)t}" : string.Empty);

        var result = await _api.ExportReportAsync(path, "stock-valuation.csv", ct);

        if (!result.IsSuccess)
        {
            var redirect = await HandleFailureAsync(result);

            if (redirect is not null)
            {
                return redirect;
            }

            TempData["ExportFailed"] = result.ErrorMessage;

            return RedirectToPage(RouteValues);
        }

        var file = result.Value!;

        return File(file.Content, file.ContentType, file.FileName);
    }

    public Dictionary<string, string> RouteValues => new()
    {
        ["type"] = ProductType?.ToString() ?? string.Empty,
    };
}
