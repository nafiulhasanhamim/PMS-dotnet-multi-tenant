using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Alerts;

/// <summary>
/// Batches past their expiry date that still hold stock.
///
/// <para>Not a warning list — a job sheet. FEFO already refuses to sell these, so nothing here is
/// at risk of going wrong; what remains is stock occupying a shelf that somebody has to
/// physically deal with.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantUser)]
public class ExpiredModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public ExpiredModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public ApiPage<ExpiringBatch> Batches { get; private set; } = ApiPage<ExpiringBatch>.Empty;

    /// <summary>
    /// Whether the caller may act on any of this. An Employee sees the list — knowing not to
    /// reach for expired stock is the point — but the actions go to screens they cannot use, so
    /// offering them would be an invitation to an access-denied page.
    /// </summary>
    /// <summary>
    /// Which batches on this page came from a recorded purchase, keyed by batch id.
    ///
    /// <para>Module 4's retrofit. One request for the whole page rather than one per row, and
    /// absent means "entered by hand" rather than "unknown".</para>
    /// </summary>
    public IReadOnlyDictionary<Guid, PurchaseOrigin> Origins { get; private set; }
        = new Dictionary<Guid, PurchaseOrigin>();

    public bool CanAct { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        CanAct = User.IsTenantAdmin() || User.Role() == UserRole.Pharmacist;

        var result = await _api.GetExpiredAsync(PageNumber, pageSize: 25, ct: ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Batches = result.Value ?? ApiPage<ExpiringBatch>.Empty;

        // Module 4's retrofit: a batch that arrived on a recorded purchase can be handed back on
        // that bill; one entered through Add Stock can only be written off. Asked for the whole
        // page at once, and a failure here leaves every row offering "Adjust stock", which is
        // the correct fallback rather than a broken page.
        if (CanAct && Batches.Data.Count > 0)
        {
            var origins = await _api.GetPurchaseOriginsAsync(
                Batches.Data.Select(b => b.BatchId).ToList(), ct);

            if (origins.IsSuccess && origins.Value is { } map)
            {
                Origins = map;
            }
        }

        return Page();
    }
}
