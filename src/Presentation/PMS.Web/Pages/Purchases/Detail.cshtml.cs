using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Purchases;

/// <summary>
/// One delivery: what arrived, what it cost, and what is still owed on it.
///
/// <para>There is no Edit button, and that is deliberate rather than unfinished. A purchase states
/// what a supplier delivered and invoiced; the stock and the balance have both moved on that
/// basis. The corrective paths are a purchase return and a stock adjustment, each of which leaves
/// a record instead of quietly replacing the original.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantWriter)]
public class DetailModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public DetailModel(PmsApiClient api) => _api = api;

    [BindProperty(SupportsGet = true, Name = "id")]
    public Guid PurchaseId { get; set; }

    public PurchaseDetail Purchase { get; private set; } = PurchaseDetail.Empty;

    public bool IsAdmin { get; private set; }

    /// <summary>Whether anything is left to send back, so the button is not offered pointlessly.</summary>
    public bool CanReturn { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        IsAdmin = User.IsTenantAdmin();

        var result = await _api.GetPurchaseAsync(PurchaseId, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? NotFound();
        }

        Purchase = result.Value ?? PurchaseDetail.Empty;

        // A line can still be returnable even when the bill is settled - returns are about goods,
        // not money - so this is asked rather than inferred from the payment status.
        var returnable = await _api.GetPurchaseReturnableLinesAsync(PurchaseId, ct);

        CanReturn = returnable.IsSuccess && (returnable.Value?.AnyReturnable ?? false);

        return Page();
    }
}
