using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Products;

/// <summary>
/// One product in full.
///
/// Readable by every pharmacy user, and prices ARE shown here even to an Employee: someone at
/// the counter needs to tell a customer what something costs. What an Employee does not get
/// is the whole price list in one download, which is why the list projection withholds them.
/// </summary>
[Authorize(Policy = WebPolicies.TenantUser)]
public class DetailModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public DetailModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public ProductModel? Product { get; private set; }

    /// <summary>Where "back" goes: the list this product belongs to.</summary>
    public string BackPage =>
        Product?.ProductType == ProductType.Medicine ? "/Medicines/Index" : "/OtherItems/Index";

    public string BackLabel =>
        Product?.ProductType == ProductType.Medicine ? "Back to medicines" : "Back to other items";

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetProductAsync(Id, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Product = result.Value;

        return Page();
    }

    /// <summary>Admin only, enforced by the API. The button is only rendered for one too.</summary>
    public async Task<IActionResult> OnPostActiveAsync(bool isActive, CancellationToken ct)
    {
        var result = await _api.SetProductActiveAsync(Id, isActive, ct);

        if (!result.IsSuccess)
        {
            var redirect = await HandleFailureAsync(result);
            if (redirect is not null)
            {
                return redirect;
            }

            await OnGetAsync(ct);
            return Page();
        }

        SuccessMessage = isActive
            ? $"{result.Value?.BrandName} is active again."
            : $"{result.Value?.BrandName} was deactivated. Existing stock and records are kept.";

        return RedirectToPage(new { id = Id });
    }
}
