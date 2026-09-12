using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Salary;

/// <summary>
/// The salary hub: four things an Admin might be here to do, and the two numbers that say
/// whether anything needs doing.
///
/// <para><b>Admin only, like every page in this module.</b> What colleagues earn is the single
/// most sensitive thing a small pharmacy stores - a counter assistant who can open this can see
/// what the person standing next to them is paid. There is no read-only variant.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class IndexModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public IndexModel(PmsApiClient api) => _api = api;

    public SalarySummary Summary { get; private set; } = SalarySummary.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetSalarySummaryAsync(ct);

        if (!result.IsSuccess)
        {
            // The cards are still worth showing. A summary that failed to load is a reason to
            // say so, not a reason to withhold the four links somebody came here to follow.
            return await HandleFailureAsync(result) ?? Page();
        }

        Summary = result.Value ?? SalarySummary.Empty;

        return Page();
    }
}
