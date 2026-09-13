using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.ViewModels;

namespace PMS.Web.Pages.Salary.Entries;

/// <summary>
/// Every month generated, what was paid and when.
///
/// <para><b>Edit disappears the moment an entry is paid</b>, and the row says why rather than
/// leaving somebody hunting for a button that used to be there. The API refuses the edit too -
/// hiding the action is a courtesy, the 409 is the rule.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class IndexModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public IndexModel(PmsApiClient api) => _api = api;

    [BindProperty(SupportsGet = true, Name = "month")]
    public int? Month { get; set; }

    [BindProperty(SupportsGet = true, Name = "year")]
    public int? Year { get; set; }

    [BindProperty(SupportsGet = true, Name = "employee")]
    public Guid? ProfileId { get; set; }

    [BindProperty(SupportsGet = true, Name = "status")]
    public SalaryStatusFilter Status { get; set; } = SalaryStatusFilter.All;

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    /// <summary>The date on the mark-as-paid form. Defaults to today.</summary>
    [BindProperty]
    [DataType(DataType.Date)]
    public DateOnly? PaymentDate { get; set; }

    public ApiPage<SalaryEntryRow> Entries { get; private set; } = ApiPage<SalaryEntryRow>.Empty;

    public IReadOnlyList<SelectListItem> Employees { get; private set; } = [];

    public IReadOnlyList<int> YearOptions { get; private set; } = [];

    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public bool HasFilters =>
        Month is not null || Year is not null || ProfileId is not null
        || Status != SalaryStatusFilter.All;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        YearOptions = Enumerable.Range(DateTime.UtcNow.Year - 3, 5).ToList();

        var options = await _api.GetSalaryProfileOptionsAsync(ct);

        if (options.IsSuccess)
        {
            Employees = (options.Value ?? [])
                .Select(o => new SelectListItem(
                    o.EmployeeName, o.ProfileId.ToString(), o.ProfileId == ProfileId))
                .ToList();
        }

        var result = await _api.GetSalaryEntriesAsync(
            Month, Year, ProfileId, Status, PageNumber, 25, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Entries = result.Value ?? ApiPage<SalaryEntryRow>.Empty;

        return Page();
    }

    public async Task<IActionResult> OnPostPayAsync(Guid id, CancellationToken ct)
    {
        var result = await _api.MarkSalaryPaidAsync(id, PaymentDate, ct);

        if (!result.IsSuccess)
        {
            var redirect = await HandleFailureAsync(result);

            // Reloaded rather than redirected, so a conflict ("already marked paid on...") shows
            // beside the row it is about.
            return redirect ?? await OnGetAsync(ct);
        }

        var paid = result.Value!;

        SuccessMessage =
            $"{Money.Format(paid.NetPayable)} paid to {paid.EmployeeName} for "
            + $"{new DateOnly(paid.Year, paid.Month, 1):MMMM yyyy}. "
            + "This entry is now locked and counts as an expense in "
            + $"{paid.PaymentDate:MMMM yyyy}.";

        return RedirectToPage("/Salary/Entries/Index", RouteValues);
    }

    public Dictionary<string, string> RouteValues => new()
    {
        ["month"] = Month?.ToString() ?? string.Empty,
        ["year"] = Year?.ToString() ?? string.Empty,
        ["employee"] = ProfileId?.ToString() ?? string.Empty,
        ["status"] = Status.ToString(),
    };
}
