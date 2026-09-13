using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Reports;

/// <summary>
/// What each supplier is owed.
///
/// <para><b>Specified with Module 8 and completed by Module 4.</b> Until suppliers and purchases
/// existed there was nothing to report, and the landing page carried a visibly disabled card
/// rather than a page returning zeros — a money report confidently saying "0.00 owed" is one
/// somebody would pay a supplier on.</para>
///
/// <para><b>Defaults to all time</b>, unlike every other report in this folder. "Who do we owe"
/// is a question about now, not about a window, so the rolling thirty days the other reports use
/// would be actively wrong here.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class SupplierDuesModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public SupplierDuesModel(PmsApiClient api) => _api = api;

    [BindProperty(SupportsGet = true, Name = "from")]
    public DateOnly? From { get; set; }

    [BindProperty(SupportsGet = true, Name = "to")]
    public DateOnly? To { get; set; }

    [BindProperty(SupportsGet = true, Name = "allTime")]
    public bool AllTime { get; set; } = true;

    public SupplierDuesReport Report { get; private set; } = SupplierDuesReport.Empty;

    public string PharmacyName { get; private set; } = "Pharmacy";

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        PharmacyName = User.TenantName() ?? "Pharmacy";

        // A date in either box means the person wants a period, whatever the checkbox says —
        // otherwise typing a date and pressing Apply appears to do nothing.
        var allTime = AllTime && From is null && To is null;

        var result = await _api.GetSupplierDuesAsync(From, To, allTime, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Report = result.Value ?? SupplierDuesReport.Empty;
        AllTime = Report.AllTime;

        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync(CancellationToken ct)
    {
        var allTime = AllTime && From is null && To is null;

        var query = new List<string> { $"allTime={allTime.ToString().ToLowerInvariant()}" };

        if (From is { } from)
        {
            query.Add($"dateFrom={from:yyyy-MM-dd}");
        }

        if (To is { } to)
        {
            query.Add($"dateTo={to:yyyy-MM-dd}");
        }

        var result = await _api.ExportReportAsync(
            "api/reports/supplier-dues/export?" + string.Join('&', query),
            "supplier-dues.csv",
            ct);

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
        ["from"] = From?.ToString("yyyy-MM-dd") ?? string.Empty,
        ["to"] = To?.ToString("yyyy-MM-dd") ?? string.Empty,
        ["allTime"] = AllTime.ToString().ToLowerInvariant(),
    };
}
