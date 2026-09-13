using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Products;

/// <summary>
/// Edit a product of either kind. The form renders medicine fields only when the product is
/// one, so the same page serves both lists.
/// </summary>
[Authorize(Policy = WebPolicies.TenantWriter)]
public class EditModel : ProductWritePageModel
{
    private readonly PmsApiClient _api;

    public EditModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public ProductModel? Product { get; private set; }

    protected override bool IsEdit => true;

    /// <summary>Cancel returns to the list the product actually belongs to.</summary>
    protected override string CancelUrl =>
        Input.IsMedicine ? "/medicines" : "/other-items";

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetProductAsync(Id, ct);

        if (!result.IsSuccess || result.Value is null)
        {
            return await HandleFailureAsync(result) ?? RedirectToPage("/Medicines/Index");
        }

        Product = result.Value;
        Input = ProductFormInput.FromProduct(result.Value);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Input.Normalise();
        ValidateLocally();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _api.UpdateProductAsync(Id, Input.ToUpdateRequest(), ct);

        if (!result.IsSuccess || result.Value is null)
        {
            var redirect = await HandleFailureAsync(result);
            return redirect ?? Page();
        }

        return RedirectToDetail(Id, $"{result.Value.BrandName} was updated.");
    }
}
