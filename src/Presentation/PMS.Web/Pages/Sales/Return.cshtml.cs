using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Sales;

/// <summary>
/// Taking goods back against one line of a sale. Admin or Pharmacist.
///
/// <para><b>The refund is shown before it is confirmed, and that is the point of the screen.</b>
/// A discount-adjusted refund is lower than the sticker price — 52.50 back on a line that reads
/// 60.00 — and a cashier who discovers that at the moment of opening the drawer will hesitate in
/// front of the customer. The figure comes from the API, computed by the same function the
/// command uses, so what is displayed is what gets paid.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantWriter)]
public class ReturnModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public ReturnModel(PmsApiClient api)
    {
        _api = api;
    }

    /// <summary>Quick-picks, plus free text. A fixed list alone gets answered "Other".</summary>
    public static readonly string[] ReasonPresets =
    [
        "Customer changed mind",
        "Wrong item given",
        "Defective",
    ];

    public ReturnableSale Sale { get; private set; } = null!;

    [BindProperty]
    public ReturnInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var loaded = await LoadAsync(id, ct);

        return loaded ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        var result = await _api.CreateReturnAsync(
            id,
            new CreateReturnPayload(
                Input.SaleLineId,
                Input.Quantity,
                Input.QuantityUnit,
                Input.Reason ?? string.Empty),
            ct);

        if (!result.IsSuccess)
        {
            var redirect = await HandleFailureAsync(result);

            if (redirect is not null)
            {
                return redirect;
            }

            // The lines have to be reloaded: the error may be about how much is left to
            // return, and the figures on screen are what the cashier will read next.
            return await LoadAsync(id, ct) ?? Page();
        }

        var returned = result.Value!;

        SuccessMessage =
            $"{returned.FormattedQuantity} of {returned.BrandName} returned to batch "
            + $"{returned.BatchNumber}. Refund {returned.RefundAmount:N2}.";

        return RedirectToPage("/Sales/Detail", new { id });
    }

    /// <summary>
    /// Loads the sale's returnable lines. Returns a redirect when the page cannot be shown —
    /// including for a cancelled sale, which the API refuses with a 409 rather than an empty
    /// list, because "nothing left to return" and "this sale was reversed entirely" are
    /// different things to tell somebody.
    /// </summary>
    private async Task<IActionResult?> LoadAsync(Guid id, CancellationToken ct)
    {
        var result = await _api.GetReturnableLinesAsync(id, ct);

        if (!result.IsSuccess)
        {
            var redirect = await HandleFailureAsync(result);

            if (redirect is not null)
            {
                return redirect;
            }

            SuccessMessage = null;
            TempData["ReturnBlocked"] = result.Problem?.Message;

            return RedirectToPage("/Sales/Detail", new { id });
        }

        Sale = result.Value!;

        return null;
    }

    protected override string ModelStateKeyFor(string apiField) => $"Input.{apiField}";
}

public sealed class ReturnInput
{
    public Guid SaleLineId { get; set; }

    public decimal Quantity { get; set; } = 1;

    public UnitLevel QuantityUnit { get; set; }

    public string? Reason { get; set; }
}
