using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Sales;

/// <summary>
/// The invoice: what the customer receives.
///
/// <para><b>Every figure here comes from the sale, not from the products.</b> That is the whole
/// point of the price snapshot — re-pricing a product next month must not change what an invoice
/// printed today says. The page never asks for a product's current price and could not use one
/// if it had it.</para>
///
/// <para>An Employee may open only their own sales; the API answers 403 for anybody else's,
/// which lands on the access-denied page through the base class.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantUser)]
public class DetailModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public DetailModel(PmsApiClient api)
    {
        _api = api;
    }

    public SaleDetail Sale { get; private set; } = null!;

    public BillingLimits Limits { get; private set; } = BillingLimits.None;
    /// <summary>
    /// The pharmacy's own name, address, phone and licence, from settings since Module 10.
    ///
    /// <para>Falls back to <c>TenantSettings.Fallback</c> if the call fails - the page still
    /// prints, and the view substitutes the cookie's tenant name for the placeholder one so the
    /// header is never blank. A document that cannot name the pharmacy is not one anybody can
    /// hand over.</para>
    /// </summary>
    public TenantSettings Pharmacy { get; private set; } = TenantSettings.Fallback;


    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var result = await _api.GetSaleAsync(id, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? RedirectToPage("/Sales/Index");
        }

        Sale = result.Value!;

        var limits = await _api.GetBillingLimitsAsync(ct);
        Limits = limits.Value ?? BillingLimits.None;

        var settings = await _api.GetSettingsAsync(ct);
        Pharmacy = settings.Value ?? TenantSettings.Fallback;

        return Page();
    }
}
