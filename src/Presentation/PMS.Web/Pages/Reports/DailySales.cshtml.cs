using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Reports;

/// <summary>
/// One day's sales and profit.
///
/// <para><b>Admin only</b>, like every page in this folder: these screens show what stock cost
/// and what the business earns on it.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class DailySalesModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public DailySalesModel(PmsApiClient api) => _api = api;

    [BindProperty(SupportsGet = true, Name = "date")]
    public DateOnly? Date { get; set; }

    public DailySalesReport Report { get; private set; } = DailySalesReport.Empty;

    public string PharmacyName { get; private set; } = "Pharmacy";

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        PharmacyName = User.TenantName() ?? "Pharmacy";

        var result = await _api.GetDailySalesReportAsync(Date, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Report = result.Value ?? DailySalesReport.Empty;
        Date = Report.Date;

        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync(CancellationToken ct)
    {
        var path = "api/reports/daily-sales/export"
            + (Date is { } d ? $"?date={d:yyyy-MM-dd}" : string.Empty);

        var result = await _api.ExportReportAsync(path, "daily-sales.csv", ct);

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
        ["date"] = Date?.ToString("yyyy-MM-dd") ?? string.Empty,
    };
}
