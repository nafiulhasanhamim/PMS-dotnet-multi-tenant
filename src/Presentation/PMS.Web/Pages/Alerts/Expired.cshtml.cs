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

        return Page();
    }
}
