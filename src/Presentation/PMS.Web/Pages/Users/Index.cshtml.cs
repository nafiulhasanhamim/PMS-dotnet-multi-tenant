using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Users;

/// <summary>
/// The pharmacy's staff list.
///
/// The <c>[Authorize]</c> attribute here is the security boundary, not the sidebar. A
/// Pharmacist who types <c>/users</c> is stopped by this, and would be stopped even if the
/// nav were rendered wrongly — nav visibility only decides what people are offered.
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class IndexModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public IndexModel(PmsApiClient api)
    {
        _api = api;
    }

    /// <summary>Requested page number, from <c>?p=2</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public PagedView<TenantUserModel> Users { get; private set; } =
        PagedView<TenantUserModel>.From(Array.Empty<TenantUserModel>(), 1);

    public string TenantName => User.TenantName() ?? "this pharmacy";

    /// <summary>The signed-in user's own id, so their row can drop its actions.</summary>
    public string? CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetUsersAsync(ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        // No tenant id is sent and none could be: the API returns whichever pharmacy the
        // stored token belongs to. There is no parameter here to get wrong.
        Users = PagedView<TenantUserModel>.From(result.Value, PageNumber);

        return Page();
    }

    public async Task<IActionResult> OnPostActiveAsync(
        Guid id, bool isActive, CancellationToken ct)
    {
        var result = await _api.SetUserActiveAsync(id, isActive, ct);

        if (!result.IsSuccess)
        {
            var redirect = await HandleFailureAsync(result);
            if (redirect is not null)
            {
                return redirect;
            }

            // Re-read so the failure appears above the real list rather than a blank page.
            var reload = await _api.GetUsersAsync(ct);
            if (reload.IsSuccess)
            {
                Users = PagedView<TenantUserModel>.From(reload.Value, PageNumber);
            }

            return Page();
        }

        var name = result.Value?.FullName ?? result.Value?.Email;

        SuccessMessage = isActive
            ? $"{name} can sign in to {TenantName} again."
            : $"{name} no longer has access to {TenantName}.";

        return RedirectToPage(new { p = PageNumber });
    }
}
