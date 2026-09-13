using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.ViewModels;

namespace PMS.Web.Pages.Stock;

/// <summary>
/// Corrects a batch's details — the things that can be mistyped off a pack.
///
/// <para><b>Quantity is not one of them.</b> It is shown, read-only, with a link to the
/// adjustment screen. That is the whole design of this page: a quantity that could be edited
/// here would be a quantity that changed with no reason recorded, and the reason is the only
/// part anybody will want in six months.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantWriter)]
public class EditBatchModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public EditBatchModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true, Name = "id")]
    public Guid BatchId { get; set; }

    [BindProperty]
    public EditBatchInput Input { get; set; } = new();

    public BatchModel? Batch { get; private set; }

    public PMS.Web.Api.ProductModel? Product { get; private set; }

    public IReadOnlyList<(UnitLevel Level, string Name)> UnitOptions =>
        Product is null ? [] : StockPresentation.UnitOptions(Product);

    public bool ExpiryRequired => Product?.ProductType == Api.ProductType.Medicine;

    /// <summary>A duplicate batch number is one field, so it renders under that input.</summary>
    protected override string? ConflictField => nameof(EditBatchInput.BatchNumber);

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
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

        Input = new EditBatchInput
        {
            BatchNumber = Batch.BatchNumber,
            ExpiryDate = Batch.ExpiryDate,
            ManufactureDate = Batch.ManufactureDate,

            // Always shown per base unit. The cost was stored per base unit, and offering it
            // back in a larger unit would mean dividing and re-multiplying a figure that is
            // frequently not a round number of paisa.
            PurchasePrice = Batch.PurchasePricePerBaseUnit,
            SupplierNameText = Batch.SupplierNameText,
            Notes = Batch.Notes,
        };

        return Page();
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

        if (string.IsNullOrWhiteSpace(Input.BatchNumber))
        {
            ModelState.AddModelError(
                "Input.BatchNumber", "Enter the batch number from the pack.");
        }

        if (ExpiryRequired && Input.ExpiryDate is null)
        {
            ModelState.AddModelError(
                "Input.ExpiryDate",
                $"{Product?.BrandName} is a medicine, so its expiry date is required.");
        }

        if (Input.ManufactureDate is { } made && Input.ExpiryDate is { } expires
            && made >= expires)
        {
            ModelState.AddModelError(
                "Input.ManufactureDate", "The manufacture date has to be before the expiry date.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _api.UpdateBatchAsync(
            BatchId,
            new UpdateBatchRequest(
                Input.BatchNumber!.Trim(),
                Input.ExpiryDate,
                Input.ManufactureDate,
                Input.PurchasePrice ?? 0,
                UnitLevel.Base,
                SupplierId: null,
                Input.SupplierNameText,
                Input.Notes,

                // The batch's own current quantity, so the server takes it as the no-op it is.
                // Sending anything else is refused, which is what makes the read-only field on
                // this page a rule rather than a decoration.
                Batch.QuantityInBaseUnits),
            ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        SuccessMessage = $"Batch {result.Value!.BatchNumber} updated.";

        return RedirectToPage("/Stock/Detail", new { productId = Batch.ProductId });
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

public sealed class EditBatchInput
{
    public string? BatchNumber { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public DateOnly? ManufactureDate { get; set; }

    /// <summary>Per base unit, always. See the page for why.</summary>
    public decimal? PurchasePrice { get; set; }

    public string? SupplierNameText { get; set; }

    public string? Notes { get; set; }
}
