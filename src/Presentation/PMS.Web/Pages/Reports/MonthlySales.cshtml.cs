using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Reports;

/// <summary>
/// One month's sales, profit and day-by-day trend.
///
/// <para>The net profit figure carries a caveat while <c>IOperatingExpenses</c> returns zero: an
/// owner reading "net profit" is entitled to assume salaries are in it, and they are not until
/// Module 9 lands. Saying so on screen is the whole of the honesty here.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class MonthlySalesModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public MonthlySalesModel(PmsApiClient api) => _api = api;

    [BindProperty(SupportsGet = true, Name = "month")]
    public int? Month { get; set; }

    [BindProperty(SupportsGet = true, Name = "year")]
    public int? Year { get; set; }

    public MonthlySalesReport Report { get; private set; } = MonthlySalesReport.Empty;

    public string PharmacyName { get; private set; } = "Pharmacy";

    /// <summary>
    /// The twelve months offered in the picker, newest first.
    ///
    /// <para>Anchored on the month being viewed rather than on today, so that paging back a year
    /// and then using the dropdown does not jump the reader forward again.</para>
    /// </summary>
    public IReadOnlyList<DateOnly> MonthOptions { get; private set; } = [];

    public DateOnly Previous => Anchor.AddMonths(-1);

    public DateOnly Next => Anchor.AddMonths(1);

    /// <summary>Whether the month after this one has started. Nothing to see if it has not.</summary>
    public bool HasNext => Next <= new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

    private DateOnly Anchor =>
        Report.Month is >= 1 and <= 12
            ? new DateOnly(Report.Year, Report.Month, 1)
            : new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        PharmacyName = User.TenantName() ?? "Pharmacy";

        var result = await _api.GetMonthlySalesReportAsync(Month, Year, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Report = result.Value ?? MonthlySalesReport.Empty;
        Month = Report.Month;
        Year = Report.Year;

        MonthOptions = Enumerable.Range(0, 12).Select(i => Anchor.AddMonths(-i)).ToList();

        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync(CancellationToken ct)
    {
        var path = $"api/reports/monthly-sales/export?month={Month}&year={Year}";

        var result = await _api.ExportReportAsync(path, "monthly-sales.csv", ct);

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
        ["month"] = Month?.ToString() ?? string.Empty,
        ["year"] = Year?.ToString() ?? string.Empty,
    };
}
