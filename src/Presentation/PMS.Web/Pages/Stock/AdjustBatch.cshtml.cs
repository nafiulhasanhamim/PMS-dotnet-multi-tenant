using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.ViewModels;

namespace PMS.Web.Pages.Stock;

/// <summary>
/// Changes a batch quantity, with a reason.
///
/// <para>The only screen in the system that can move stock outside a sale, and the reason
/// field is not optional. A quantity that changed with no explanation records that the numbers
/// moved and destroys the only thing anybody will later want to know about it.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantWriter)]
public class AdjustBatchModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public AdjustBatchModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true, Name = "id")]
    public Guid BatchId { get; set; }

    [BindProperty]
    public AdjustBatchInput Input { get; set; } = new();

    public BatchModel? Batch { get; private set; }

    public PMS.Web.Api.ProductModel? Product { get; private set; }

    public IReadOnlyList<(UnitLevel Level, string Name)> UnitOptions =>
        Product is null ? [] : StockPresentation.UnitOptions(Product);

    /// <summary>
    /// Whether adding to this batch needs an acknowledgement first: it has expired, or is
    /// close to it.
    ///
    /// <para>Thirty days, much shorter than the ninety-day amber window on the lists. Adding
    /// to a batch that expires in two months is ordinary — a miscount corrected. Adding to one
    /// that expires this month almost always means fresh stock arrived and somebody reached
    /// for the nearest existing row, which silently gives the new delivery the old one's
    /// expiry date and cost.</para>
    /// </summary>
    public bool IsNearExpiry =>
        Batch?.DaysUntilExpiry is { } days && days <= 30;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var redirect = await LoadAsync(ct);
        if (redirect is not null)
        {
            return redirect;
        }

        return Batch is null ? RedirectToPage("/Stock/Index") : Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var redirect = await LoadAsync(ct);
        if (redirect is not null)
        {
            return redirect;
        }

        if (Batch is null)
        {
            return RedirectToPage("/Stock/Index");
        }

        if (string.IsNullOrWhiteSpace(Input.Reason))
        {
            ModelState.AddModelError("Input.Reason", "Say why the quantity is changing.");
        }

        if (Input.AdjustmentType == AdjustmentType.Correction)
        {
            // A correction to zero is legitimate: the shelf is empty and the system thinks
            // otherwise, which is exactly the discrepancy this screen exists for.
            if (Input.Quantity is null or < 0)
            {
                ModelState.AddModelError("Input.Quantity", "Enter the correct quantity.");
            }
        }
        else if (Input.Quantity is null or <= 0)
        {
            ModelState.AddModelError("Input.Quantity", "Enter how much to add or remove.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _api.AdjustBatchAsync(
            BatchId,
            new AdjustBatchRequest(
                Input.AdjustmentType,
                Input.Quantity ?? 0,
                Input.QuantityUnit,
                Input.Reason!.Trim(),

                // Only ever true because the person ticked the box on the warning. The API
                // refuses the addition without it, so this is a real gate rather than a
                // message the page could choose not to show.
                Input.AcknowledgeExpiringBatch),
            ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        var adjusted = result.Value!;

        SuccessMessage =
            $"Batch {adjusted.Batch.BatchNumber} adjusted: "
            + $"{adjusted.Adjustment.FormattedQuantityChange}, now "
            + $"{adjusted.Batch.FormattedQuantity}.";

        return RedirectToPage("/Stock/Detail", new { productId = adjusted.Batch.ProductId });
    }

    private async Task<IActionResult?> LoadAsync(CancellationToken ct)
    {
        var batch = await _api.GetBatchAsync(BatchId, ct);

        if (!batch.IsSuccess)
        {
            if (batch.Problem?.Status == StatusCodes.Status404NotFound)
            {
                return RedirectToPage("/Stock/Index");
            }

            return await HandleFailureAsync(batch);
        }

        Batch = batch.Value;

        var product = await _api.GetProductAsync(Batch!.ProductId, ct);

        if (!product.IsSuccess)
        {
            return await HandleFailureAsync(product);
        }

        Product = product.Value;

        return null;
    }
}

public sealed class AdjustBatchInput
{
    public AdjustmentType AdjustmentType { get; set; } = AdjustmentType.Remove;

    /// <summary>
    /// For Add and Remove, how much to move. For Correction, the true total — the server works
    /// out the difference, because somebody who has just counted a shelf knows what is on it.
    /// </summary>
    public decimal? Quantity { get; set; }

    public UnitLevel QuantityUnit { get; set; } = UnitLevel.Base;

    public string? Reason { get; set; }

    public bool AcknowledgeExpiringBatch { get; set; }
}
