using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.ViewModels;

namespace PMS.Web.Pages.Alerts;

/// <summary>
/// The alerts hub.
///
/// <para>The same four cards as the dashboard, larger and with a line of explanation each. It
/// exists so alerts are a place in the application rather than four tiles that happen to be on
/// the home page — somebody who has navigated away needs a way back that is not "go to the
/// dashboard and look for the box".</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantUser)]
public class IndexModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public IndexModel(PmsApiClient api)
    {
        _api = api;
    }

    public AlertSummary Summary { get; private set; } = AlertSummary.Empty;

    public IReadOnlyList<AlertCardModel> Cards { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetAlertSummaryAsync(ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Summary = result.Value ?? AlertSummary.Empty;
        Cards = AlertCardModel.From(Summary);

        return Page();
    }
}
