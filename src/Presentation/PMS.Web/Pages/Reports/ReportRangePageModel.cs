using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Reports;

/// <summary>
/// Shared behaviour for the reports that take a date range.
///
/// <para>Four pages bind the same two dates, default them the same way and pass the same filters
/// to their export. Writing that four times is how one of them ends up defaulting to a different
/// window than the others, and two reports over "the last month" that disagree are worse than one
/// report.</para>
/// </summary>
public abstract class ReportRangePageModel : PmsPageModel
{
    /// <summary>Matches the server's <c>ReportRange.DefaultDays</c>.</summary>
    public const int DefaultDays = 30;

    [BindProperty(SupportsGet = true, Name = "from")]
    public DateOnly? From { get; set; }

    [BindProperty(SupportsGet = true, Name = "to")]
    public DateOnly? To { get; set; }

    public string PharmacyName { get; private set; } = "Pharmacy";

    /// <summary>
    /// Fills in an omitted end of the range and reads the pharmacy name off the session.
    ///
    /// <para>The dates are resolved here as well as on the server, and deliberately: the server
    /// is the authority on what it queried, but the page has to put something in its two date
    /// inputs, and leaving them blank while showing thirty days of data would misdescribe the
    /// table underneath. Both sides use the same window and the same inclusive-both-ends rule.</para>
    /// </summary>
    protected void Prepare()
    {
        PharmacyName = User.TenantName() ?? "Pharmacy";

        // UtcNow, because the API bounds its ranges in UTC. Using the display zone here would put
        // a different date in the input than the one the server filtered on.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        To ??= today;
        From ??= To.Value.AddDays(-(DefaultDays - 1));

        if (From > To)
        {
            (From, To) = (To, From);
        }
    }

    public virtual Dictionary<string, string> RouteValues => new()
    {
        ["from"] = From?.ToString("yyyy-MM-dd") ?? string.Empty,
        ["to"] = To?.ToString("yyyy-MM-dd") ?? string.Empty,
    };

    /// <summary>
    /// Streams a CSV back, or returns to the report with the reason it could not.
    ///
    /// <para>A failed download sends the person back to the report rather than to an error page:
    /// they still want the report, they just did not get the file.</para>
    /// </summary>
    protected async Task<IActionResult> ExportAsync(
        PmsApiClient api, string path, string fallbackName, CancellationToken ct)
    {
        var result = await api.ExportReportAsync(path, fallbackName, ct);

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
}
