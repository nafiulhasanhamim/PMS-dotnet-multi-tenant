using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Reports;

/// <summary>
/// Stock that is not moving, and stock that never has.
///
/// <para>The never-sold rows are the ones worth acting on and they sort first. A product that has
/// sat on a shelf since it was bought is capital the pharmacy could have spent on something that
/// sells, and unlike a slow seller there is no trend to wait out.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class DeadStockModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public DeadStockModel(PmsApiClient api) => _api = api;

    /// <summary>Mirrors the server's <c>StockPolicy.SelectableDeadStockThresholds</c>.</summary>
    public static readonly int[] Thresholds = [30, 60, 90, 180];

    [BindProperty(SupportsGet = true, Name = "days")]
    public int? ThresholdDays { get; set; }

    [BindProperty(SupportsGet = true, Name = "type")]
    public ProductType? ProductType { get; set; }

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public DeadStockPage Report { get; private set; } = DeadStockPage.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetDeadStockAsync(
            ThresholdDays, ProductType, PageNumber, pageSize: 25, ct: ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Report = result.Value ?? DeadStockPage.Empty;

        // Echo back what the server actually applied, not what was asked for. The two differ
        // whenever somebody edits the query string, and a filter control showing the rejected
        // value would describe the table wrongly.
        ThresholdDays = Report.ThresholdDays;

        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync(CancellationToken ct)
    {
        var path = "api/reports/dead-stock/export?"
            + PmsApiClient.DeadStockQuery(ThresholdDays, ProductType);

        var result = await _api.ExportReportAsync(path, "dead-stock.csv", ct);

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
        ["days"] = ThresholdDays?.ToString() ?? string.Empty,
        ["type"] = ProductType?.ToString() ?? string.Empty,
    };
}
