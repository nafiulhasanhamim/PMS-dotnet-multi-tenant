using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.Pages.Products;

namespace PMS.Web.Pages.OtherItems;

/// <summary>
/// Add something that is not a medicine.
///
/// No catalogue import path: the reference catalogue covers medicines only. The form does not
/// render generic name, strength, dosage form or the antibiotic checkbox at all, which is why
/// those fields cannot be filled in by accident here.
/// </summary>
[Authorize(Policy = WebPolicies.TenantWriter)]
public class CreateModel : ProductWritePageModel
{
    private readonly PmsApiClient _api;

    public CreateModel(PmsApiClient api)
    {
        _api = api;
    }

    protected override bool IsEdit => false;

    protected override string CancelUrl => "/other-items";

    public void OnGet() => Input = ProductFormInput.NewFor(ProductType.PersonalCare);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        // A medicine cannot be created from this form: it renders none of the fields a
        // medicine requires, so allowing the type through would produce a request the API
        // rejects for reasons the person cannot see or fix here.
        if (Input.ProductType == ProductType.Medicine)
        {
            Input.ProductType = ProductType.Other;
        }

        Input.Normalise();
        ValidateLocally();

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

        return RedirectToDetail(result.Value.Id, $"{result.Value.BrandName} was added.");
    }
}
