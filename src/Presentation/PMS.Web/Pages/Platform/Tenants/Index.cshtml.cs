using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.ViewModels;

namespace PMS.Web.Pages.Platform.Tenants;

[Authorize(Policy = WebPolicies.PlatformAdmin)]
public class IndexModel : PlatformPageModel
{
    private readonly PmsApiClient _api;

    public IndexModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public PagedView<TenantModel> Tenants { get; private set; } =
        PagedView<TenantModel>.From(Array.Empty<TenantModel>(), 1);

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetTenantsAsync(ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Tenants = PagedView<TenantModel>.From(result.Value, PageNumber);

        return Page();
    }

    public async Task<IActionResult> OnPostStatusAsync(
        Guid id, TenantStatus status, CancellationToken ct)
    {
        var result = await _api.UpdateTenantStatusAsync(id, status, ct);

        if (!result.IsSuccess)
        {
            var redirect = await HandleFailureAsync(result);
            if (redirect is not null)
            {
                return redirect;
            }

            var reload = await _api.GetTenantsAsync(ct);
            if (reload.IsSuccess)
            {
                Tenants = PagedView<TenantModel>.From(reload.Value, PageNumber);
            }

            return Page();
        }

        SuccessMessage = status == TenantStatus.Suspended
            ? $"{result.Value?.Name} is suspended. Nobody there can sign in until it is "
              + "reactivated."
            : $"{result.Value?.Name} is now {StatusPresentation.ForTenant(status).Text
                .ToLowerInvariant()}.";

        return RedirectToPage(new { p = PageNumber });
    }
}
