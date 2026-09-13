using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Suppliers;

/// <summary>
/// One supplier: what they are owed, what has been bought from them, and what has been paid.
///
/// <para>The two history tables page independently, so looking further back through payments does
/// not reset the purchase list.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantWriter)]
public class DetailModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public DetailModel(PmsApiClient api) => _api = api;

    [BindProperty(SupportsGet = true, Name = "id")]
    public Guid SupplierId { get; set; }

    [BindProperty(SupportsGet = true, Name = "pp")]
    public int PurchasePage { get; set; } = 1;

    [BindProperty(SupportsGet = true, Name = "yp")]
    public int PaymentPage { get; set; } = 1;

    public SupplierDetail Supplier { get; private set; } = SupplierDetail.Empty;

    public ApiPage<SupplierPurchaseRow> Purchases { get; private set; }
        = ApiPage<SupplierPurchaseRow>.Empty;

    public ApiPage<SupplierPaymentRow> Payments { get; private set; }
        = ApiPage<SupplierPaymentRow>.Empty;

    /// <summary>Payments and deactivation are Admin-only; the buttons follow the policy.</summary>
    public bool IsAdmin { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        IsAdmin = User.IsTenantAdmin();

        var result = await _api.GetSupplierAsync(SupplierId, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? NotFound();
        }

        Supplier = result.Value ?? SupplierDetail.Empty;

        var purchases = await _api.GetSupplierPurchasesAsync(SupplierId, PurchasePage, 20, ct);

        if (purchases.IsSuccess)
        {
            Purchases = purchases.Value ?? ApiPage<SupplierPurchaseRow>.Empty;
        }

        var payments = await _api.GetSupplierPaymentsAsync(SupplierId, PaymentPage, 20, ct);

        if (payments.IsSuccess)
        {
            Payments = payments.Value ?? ApiPage<SupplierPaymentRow>.Empty;
        }

        return Page();
    }

    /// <summary>Deactivates or reactivates. Admin only — the API refuses anyone else anyway.</summary>
    public async Task<IActionResult> OnPostSetStatusAsync(bool active, CancellationToken ct)
    {
        var result = await _api.SetSupplierActiveAsync(SupplierId, active, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? await OnGetAsync(ct);
        }

        SuccessMessage = active
            ? $"{result.Value!.Name} is active again."
            : $"{result.Value!.Name} has been deactivated. Their purchase history is unchanged.";

        return RedirectToPage(new { id = SupplierId });
    }

    public Dictionary<string, string> PurchaseRoute => new()
    {
        ["id"] = SupplierId.ToString(),
        ["yp"] = PaymentPage.ToString(),
    };

    public Dictionary<string, string> PaymentRoute => new()
    {
        ["id"] = SupplierId.ToString(),
        ["pp"] = PurchasePage.ToString(),
    };
}
