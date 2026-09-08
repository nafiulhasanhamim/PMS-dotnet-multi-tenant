using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.Tenancy;
using PMS.Web.ViewModels;

namespace PMS.Web.Pages.Platform.Tenants;

[Authorize(Policy = WebPolicies.PlatformAdmin)]
public class DetailModel : PlatformPageModel
{
    private readonly PmsApiClient _api;
    private readonly ITenantHostResolver _hosts;

    public DetailModel(PmsApiClient api, ITenantHostResolver hosts)
    {
        _api = api;
        _hosts = hosts;
    }

    /// <summary>The address this pharmacy's staff sign in at.</summary>
    public string? SignInUrl => Tenant is null
        ? null
        : _hosts.SignInUrl(Tenant.DomainName, Request.IsHttps, Request.Host.Port);

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public TenantModel? Tenant { get; private set; }

    /// <summary>
    /// The pharmacy's existing staff. Read through the platform endpoint, which takes the
    /// tenant id explicitly — a platform operator is inside no pharmacy, so the tenant query
    /// filter would show them nothing.
    /// </summary>
    public IReadOnlyList<TenantUserModel> Admins { get; private set; } =
        Array.Empty<TenantUserModel>();

    [BindProperty]
    public InputModel Input { get; set; } = new();

    // A duplicate-membership 409 from the admin form is about the email that was typed.
    protected override string? ConflictField => nameof(InputModel.Email);

    public sealed class InputModel
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var redirect = await LoadAsync(ct);

        return redirect ?? Page();
    }

    public async Task<IActionResult> OnPostStatusAsync(TenantStatus status, CancellationToken ct)
    {
        var result = await _api.UpdateTenantStatusAsync(Id, status, ct);

        if (!result.IsSuccess)
        {
            var redirect = await HandleFailureAsync(result);
            if (redirect is not null)
            {
                return redirect;
            }

            await LoadAsync(ct);
            return Page();
        }

        SuccessMessage = status == TenantStatus.Suspended
            ? $"{result.Value?.Name} is suspended. Nobody there can sign in until it is "
              + "reactivated."
            : $"{result.Value?.Name} is now {StatusPresentation.ForTenant(status).Text
                .ToLowerInvariant()}.";

        return RedirectToPage(new { id = Id });
    }

    public async Task<IActionResult> OnPostCreateAdminAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Input.Email))
        {
            ModelState.AddModelError("Input.Email", "Enter an email address.");
        }

        if (string.IsNullOrWhiteSpace(Input.FullName))
        {
            ModelState.AddModelError("Input.FullName", "Enter a full name.");
        }

        if (string.IsNullOrWhiteSpace(Input.Password))
        {
            ModelState.AddModelError("Input.Password", "Enter a password.");
        }
        else if (Input.Password.Length < 8)
        {
            ModelState.AddModelError("Input.Password", "Use at least 8 characters.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(ct);
            return Page();
        }

        var result = await _api.CreateTenantAdminAsync(
            Id,
            new CreateTenantAdminRequest(Input.Email.Trim(), Input.FullName.Trim(), Input.Password),
            ct);

        if (!result.IsSuccess || result.Value is null)
        {
            var redirect = await HandleFailureAsync(result);
            if (redirect is not null)
            {
                return redirect;
            }

            await LoadAsync(ct);
            return Page();
        }

        // These two outcomes must not read alike. On the linked path the password just typed
        // was never applied, and a platform admin who passes it on has created a support call
        // and a colleague who cannot sign in. So the message says so outright.
        SuccessMessage = result.Value.Outcome == ProvisioningOutcome.ExistingUserLinked
            ? $"{result.Value.Email} already had a PMS account, and has been linked to this "
              + "pharmacy as an administrator. Important: the password you entered was not "
              + "applied — they sign in with the password they already use."
            : $"A new administrator account was created for {result.Value.Email}. They can "
              + "sign in with the password you entered.";

        return RedirectToPage(new { id = Id });
    }

    private async Task<IActionResult?> LoadAsync(CancellationToken ct)
    {
        var tenant = await _api.GetTenantAsync(Id, ct);

        if (!tenant.IsSuccess)
        {
            var redirect = await HandleFailureAsync(tenant);
            if (redirect is not null)
            {
                return redirect;
            }

            // A 404 here means the pharmacy is gone; nothing on the page makes sense.
            ShowError("That pharmacy no longer exists.");
            return null;
        }

        Tenant = tenant.Value;

        var users = await _api.GetTenantUsersAsync(Id, ct);

        if (users.IsSuccess)
        {
            Admins = users.Value ?? (IReadOnlyList<TenantUserModel>)Array.Empty<TenantUserModel>();
        }
        else
        {
            // A failure listing staff must not blank out the rest of the page — status
            // management still works, and that is the more urgent control.
            ShowError(users.ErrorMessage);
        }

        return null;
    }
}
