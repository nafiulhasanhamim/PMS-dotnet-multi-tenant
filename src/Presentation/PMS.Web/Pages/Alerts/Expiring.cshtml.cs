using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Alerts;

/// <summary>
/// Batches expiring inside the selected window, soonest first.
///
/// <para>Read-only, like everything in this module. The row links out to the product's stock
/// page, which is where the two things a person can actually do live — adjust the quantity, or
/// look at the batch in context.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantUser)]
public class ExpiringModel : PmsPageModel
{
    /// <summary>
    /// Mirrors <c>StockPolicy.SelectableExpiryWindows</c>. Duplicated because this app
    /// references no other project — the API coerces anything outside the set back to the
    /// default, so a drifted copy here shows the wrong dropdown rather than producing a wrong
    /// list.
    /// </summary>
    public static readonly int[] Windows = [30, 60, 90, 180];

    private readonly PmsApiClient _api;

    public ExpiringModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true, Name = "days")]
    public int? Days { get; set; }

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public ApiPage<ExpiringBatch> Batches { get; private set; } = ApiPage<ExpiringBatch>.Empty;

    /// <summary>The window the API actually used, echoed back so the heading cannot lie.</summary>
    public int WindowDays { get; private set; } = 90;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var summary = await _api.GetAlertSummaryAsync(ct);
        WindowDays = Days ?? summary.Value?.ExpiryWindowDays ?? 90;

        var result = await _api.GetExpiringAsync(Days, PageNumber, pageSize: 25, ct: ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Batches = result.Value ?? ApiPage<ExpiringBatch>.Empty;

        return Page();
    }

    public Dictionary<string, string> RouteValues => new()
    {
        ["days"] = Days?.ToString() ?? string.Empty,
    };
}
