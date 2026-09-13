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
    /// The four standard windows, mirroring <c>StockPolicy.StandardExpiryWindows</c>. Duplicated
    /// because this app references no other project — the API coerces anything outside the set
    /// back to the pharmacy's own, so a drifted copy here shows the wrong dropdown rather than
    /// producing a wrong list.
    /// </summary>
    public static readonly int[] StandardWindows = [30, 60, 90, 180];

    /// <summary>
    /// What the dropdown offers: the four standard windows plus this pharmacy's own, where that
    /// is something else.
    ///
    /// <para>A pharmacy that configured 45 days and then found the dropdown could not show 45
    /// would have a settings screen its own alert page disagreed with. Same rule as
    /// <c>StockPolicy.SelectableExpiryWindows</c>.</para>
    /// </summary>
    public IReadOnlyList<int> Windows =>
        StandardWindows.Contains(ConfiguredWindowDays)
            ? StandardWindows
            : StandardWindows.Append(ConfiguredWindowDays).Order().ToList();

    /// <summary>This pharmacy's configured window, which is what "All" falls back to.</summary>
    public int ConfiguredWindowDays { get; private set; } =
        TenantSettings.Fallback.ExpiryAlertWindowDays;

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
    public int WindowDays { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        // The summary reports the pharmacy's configured window - it is the number it counted
        // with - so this needs no literal of its own. The fallback below only applies when that
        // call failed, and it is TenantSettings.Fallback's rather than one invented here.
        var summary = await _api.GetAlertSummaryAsync(ct);

        ConfiguredWindowDays = summary.Value?.ExpiryWindowDays
            ?? TenantSettings.Fallback.ExpiryAlertWindowDays;

        WindowDays = Days ?? ConfiguredWindowDays;

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
