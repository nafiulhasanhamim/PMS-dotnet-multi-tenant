using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Salary.Advances;

/// <summary>
/// Every advance handed over, settled and unsettled together.
///
/// <para>The two are shown in one list rather than split, because the question somebody brings
/// here is usually about one employee across both states: "what has Karim had, and what is still
/// to come out?" The settled rows name the month that recovered them.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class IndexModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public IndexModel(PmsApiClient api) => _api = api;

    [BindProperty(SupportsGet = true, Name = "employee")]
    public Guid? ProfileId { get; set; }

    [BindProperty(SupportsGet = true, Name = "settlement")]
    public AdvanceSettlementFilter Settlement { get; set; } = AdvanceSettlementFilter.All;

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public ApiPage<SalaryAdvanceRow> Advances { get; private set; }
        = ApiPage<SalaryAdvanceRow>.Empty;

    public IReadOnlyList<SelectListItem> Employees { get; private set; } = [];

    public bool HasFilters =>
        ProfileId is not null || Settlement != AdvanceSettlementFilter.All;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var options = await _api.GetSalaryProfileOptionsAsync(ct);

        if (options.IsSuccess)
        {
            Employees = (options.Value ?? [])
                .Select(o => new SelectListItem(
                    o.EmployeeName, o.ProfileId.ToString(), o.ProfileId == ProfileId))
                .ToList();
        }

        var result = await _api.GetSalaryAdvancesAsync(
            ProfileId, Settlement, PageNumber, 25, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Advances = result.Value ?? ApiPage<SalaryAdvanceRow>.Empty;

        return Page();
    }

    public Dictionary<string, string> RouteValues => new()
    {
        ["employee"] = ProfileId?.ToString() ?? string.Empty,
        ["settlement"] = Settlement.ToString(),
    };
}
