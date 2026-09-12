using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Purchases;

/// <summary>
/// Sends goods back to the supplier.
///
/// <para>Admin and Pharmacist: this is a stock event somebody is standing there performing, and
/// making them find an Admin first is how stock records stop matching the shelf.</para>
///
/// <para>Every line is shown, including the ones with nothing left to return — disabled, with the
/// reason. Hiding them would leave somebody hunting for a product they can see on the purchase.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantWriter)]
public class ReturnModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public ReturnModel(PmsApiClient api) => _api = api;

    /// <summary>The quick-picks beside the reason box. Free text is still accepted.</summary>
    public static readonly string[] ReasonPresets =
        ["Near expiry", "Damaged on arrival", "Wrong item", "Other"];

    [BindProperty(SupportsGet = true, Name = "id")]
    public Guid PurchaseId { get; set; }

    /// <summary>Pre-selects a line, so the expired-stock page can link straight to one.</summary>
    [BindProperty(SupportsGet = true, Name = "line")]
    public Guid? PreselectedLineId { get; set; }

    [BindProperty]
    public ReturnInput Input { get; set; } = new();

    public ReturnablePurchase Purchase { get; private set; } = ReturnablePurchase.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var loaded = await LoadAsync(ct);

        if (loaded is not null)
        {
            return loaded;
        }

        Input.PurchaseLineId = PreselectedLineId
            ?? Purchase.Lines.FirstOrDefault(l => l.CanReturn)?.PurchaseLineId;

        Input.QuantityUnit = UnitLevel.Base;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var loaded = await LoadAsync(ct);

        if (loaded is not null)
        {
            return loaded;
        }

        if (Input.PurchaseLineId is null)
        {
            ModelState.AddModelError("Input.PurchaseLineId", "Choose which item is going back.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _api.CreatePurchaseReturnAsync(
            PurchaseId,
            new CreatePurchaseReturnPayload(
                Input.PurchaseLineId!.Value, Input.Quantity, Input.QuantityUnit, Input.Reason!),
            ct);

        if (!result.IsSuccess)
        {
            // The API owns the returnable cap, so its message - which names the maximum and says
            // whether stock or the bill is the limit - is what the person sees.
            return await HandleFailureAsync(result) ?? Page();
        }

        var returned = result.Value!;

        SuccessMessage =
            $"{returned.FormattedQuantityReturned} returned. "
            + $"The batch now holds {returned.FormattedBatchQuantityAfter}, and "
            + $"{returned.ReturnAmount:N2} has come off what this supplier is owed.";

        return RedirectToPage("/Purchases/Detail", new { id = PurchaseId });
    }

    private async Task<IActionResult?> LoadAsync(CancellationToken ct)
    {
        var result = await _api.GetPurchaseReturnableLinesAsync(PurchaseId, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? NotFound();
        }

        Purchase = result.Value ?? ReturnablePurchase.Empty;

        return null;
    }

    public sealed class ReturnInput
    {
        public Guid? PurchaseLineId { get; set; }

        [Range(0.0001, 99999999, ErrorMessage = "Enter how much is going back.")]
        public decimal Quantity { get; set; }

        public UnitLevel QuantityUnit { get; set; } = UnitLevel.Base;

        [Required(ErrorMessage = "Say why this is going back.")]
        [StringLength(400)]
        public string? Reason { get; set; }
    }
}
