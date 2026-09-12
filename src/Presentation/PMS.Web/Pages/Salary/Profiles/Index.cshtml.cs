using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Salary.Profiles;

/// <summary>
/// Who is on the payroll, and what each still owes back in advances.
///
/// <para>Deactivating is a soft delete and the confirmation says so: every salary entry and every
/// advance ever recorded points at the profile, and removing it would mean deleting the record of
/// money that left the till.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class IndexModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public IndexModel(PmsApiClient api) => _api = api;

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "status")]
    public SalaryProfileStatusFilter Status { get; set; } = SalaryProfileStatusFilter.Active;

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public ApiPage<SalaryProfileListItem> Profiles { get; private set; }
        = ApiPage<SalaryProfileListItem>.Empty;

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Search) || Status != SalaryProfileStatusFilter.Active;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetSalaryProfilesAsync(Search, Status, PageNumber, 25, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Profiles = result.Value ?? ApiPage<SalaryProfileListItem>.Empty;

        return Page();
    }

    public async Task<IActionResult> OnPostStatusAsync(
        Guid id, bool isActive, CancellationToken ct)
    {
        var result = await _api.SetSalaryProfileStatusAsync(id, isActive, ct);

        if (!result.IsSuccess)
        {
            var redirect = await HandleFailureAsync(result);

            if (redirect is not null)
            {
                return redirect;
            }

            // Reload so the failure renders against the real list rather than an empty page -
            // a conflict here ("they already have an active profile") only makes sense beside
            // the rows it is talking about.
            return await OnGetAsync(ct);
        }

        SuccessMessage = isActive
            ? $"{result.Value!.EmployeeName} is back on the payroll."
            : $"{result.Value!.EmployeeName} is off the payroll. Their salary history is unchanged.";

        return RedirectToPage("/Salary/Profiles/Index", RouteValues);
    }

    public Dictionary<string, string> RouteValues => new()
    {
        ["q"] = Search ?? string.Empty,
        ["status"] = Status.ToString(),
    };
}
