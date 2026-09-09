using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.Pages.Products;

namespace PMS.Web.Pages.Medicines;

/// <summary>
/// Add a medicine, either by hand or as step 2 of a catalogue import.
///
/// One page for both: with a <c>catalogId</c> it pre-fills from the reference catalogue and
/// shows the review framing; without one it is a blank form. Same validation, same POST, so
/// an imported product differs from a hand-entered one only by the link it carries.
/// </summary>
[Authorize(Policy = WebPolicies.TenantWriter)]
public class CreateModel : ProductWritePageModel
{
    private readonly PmsApiClient _api;

    public CreateModel(PmsApiClient api)
    {
        _api = api;
    }

    /// <summary>Set by the import flow. Null for a manual entry.</summary>
    [BindProperty(SupportsGet = true, Name = "catalogId")]
    public int? CatalogId { get; set; }

    protected override bool IsEdit => false;

    protected override string CancelUrl => "/medicines";

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (CatalogId is null)
        {
            Input = ProductFormInput.NewFor(ProductType.Medicine);
            return Page();
        }

        var entry = await _api.GetCatalogMedicineAsync(CatalogId.Value, ct);

        if (!entry.IsSuccess || entry.Value is null)
        {
            var redirect = await HandleFailureAsync(entry);
            if (redirect is not null)
            {
                return redirect;
            }

            // The catalogue entry has gone. Fall back to a blank form rather than a dead end -
            // the medicine still needs adding.
            ShowError("That catalog entry could not be loaded. You can still add the medicine "
                      + "manually below.");
            Input = ProductFormInput.NewFor(ProductType.Medicine);
            return Page();
        }

        FromCatalog = entry.Value;
        Input = ProductFormInput.FromCatalog(entry.Value);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Input.ProductType = ProductType.Medicine;
        Input.Normalise();
        ValidateLocally();

        // Re-read the catalogue entry so a redisplayed form keeps its review framing.
        if (Input.CatalogMedicineId is not null)
        {
            var entry = await _api.GetCatalogMedicineAsync(Input.CatalogMedicineId.Value, ct);
            FromCatalog = entry.IsSuccess ? entry.Value : null;
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _api.CreateProductAsync(Input.ToCreateRequest(), ct);

        if (!result.IsSuccess || result.Value is null)
        {
            var redirect = await HandleFailureAsync(result);
            return redirect ?? Page();
        }

        return RedirectToDetail(
            result.Value.Id,
            Input.CatalogMedicineId is not null
                ? $"{result.Value.BrandName} was imported from the reference catalog."
                : $"{result.Value.BrandName} was added.");
    }
}
