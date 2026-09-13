using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Platform;

/// <summary>
/// A separate page hitting a separate API endpoint and issuing a separate cookie.
///
/// A platform token carries no tenant, so it can never satisfy a tenant-scoped request. Two
/// pages make that distinction visible rather than hiding it behind a blank domain field —
/// and signing in here does not sign you out of a pharmacy, because the two cookies are
/// independent.
/// </summary>
[AllowAnonymous]
public class LoginModel : PlatformPageModel
{
    private readonly PmsApiClient _api;

    public LoginModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public bool Expired { get; set; }

    /// <summary>See the tenant login page — a returnUrl means the visitor was turned away.</summary>
    [BindProperty(SupportsGet = true, Name = "returnUrl")]
    public string? ReturnUrl { get; set; }

    public bool ShowSessionEnded => Expired || !string.IsNullOrEmpty(ReturnUrl);

    public sealed class InputModel
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (User.IsPlatformAdmin())
        {
            return RedirectToPage("/Platform/Tenants/Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Input.Email))
        {
            ModelState.AddModelError("Input.Email", "Enter your email.");
        }

        if (string.IsNullOrWhiteSpace(Input.Password))
        {
            ModelState.AddModelError("Input.Password", "Enter your password.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _api.PlatformLoginAsync(
            new PlatformLoginRequest(Input.Email.Trim(), Input.Password), ct);

        if (!result.IsSuccess || result.Value is null)
        {
            ShowError(result.ErrorMessage);
            return Page();
        }

        await HttpContext.SignInPlatformAdminAsync(result.Value);

        return RedirectToPage("/Platform/Tenants/Index");
    }
}
