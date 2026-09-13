using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Salary.Entries;

/// <summary>
/// The printable salary slip, following the invoice's print approach from Module 5.
///
/// <para>Available on an unpaid entry too. Handing somebody the breakdown before the money moves
/// is how a disagreement gets settled before it becomes one - and an unpaid slip says so on its
/// face, so it cannot be mistaken for a receipt.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class SlipModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public SlipModel(PmsApiClient api) => _api = api;

    [BindProperty(SupportsGet = true, Name = "id")]
    public Guid EntryId { get; set; }

    public SalarySlip Slip { get; private set; } = null!;
    /// <summary>
    /// The pharmacy's own name, address, phone and licence, from settings since Module 10.
    ///
    /// <para>Falls back to <c>TenantSettings.Fallback</c> if the call fails - the page still
    /// prints, and the view substitutes the cookie's tenant name for the placeholder one so the
    /// header is never blank. A document that cannot name the pharmacy is not one anybody can
    /// hand over.</para>
    /// </summary>
    public TenantSettings Pharmacy { get; private set; } = TenantSettings.Fallback;


    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetSalarySlipAsync(EntryId, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? NotFound();
        }

        Slip = result.Value!;

        var settings = await _api.GetSettingsAsync(ct);
        Pharmacy = settings.Value ?? TenantSettings.Fallback;

        return Page();
    }
}
