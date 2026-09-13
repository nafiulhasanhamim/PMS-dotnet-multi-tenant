using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Platform.Tenants;

[Authorize(Policy = WebPolicies.PlatformAdmin)]
public class CreateModel : PlatformPageModel
{
    private readonly PmsApiClient _api;

    public CreateModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    // The API's uniqueness check is authoritative and answers 409. That is a problem with one
    // input, not with the form, so it renders under the domain name field.
    protected override string? ConflictField => nameof(InputModel.DomainName);

    /// <summary>
    /// Free text on the API today, so the list here is this app's own suggestion rather than
    /// a contract. Kept as a dropdown because a typed plan name is a plan name nobody can
    /// report on; "Other" stays available through the API for anything unusual.
    /// </summary>
    public static IReadOnlyList<string> Plans { get; } = new[]
    {
        "Trial",
        "Standard",
        "Professional",
        "Enterprise",
    };

    public sealed class InputModel
    {
        public string Name { get; set; } = string.Empty;
        public string DomainName { get; set; } = string.Empty;
        public string? SubscriptionPlan { get; set; } = "Standard";
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Input.Name))
        {
            ModelState.AddModelError("Input.Name", "Enter the pharmacy's name.");
        }

        if (string.IsNullOrWhiteSpace(Input.DomainName))
        {
            ModelState.AddModelError("Input.DomainName", "Enter a domain name.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _api.CreateTenantAsync(
            new CreateTenantRequest(
                Input.Name.Trim(),
                Input.DomainName.Trim(),
                string.IsNullOrWhiteSpace(Input.SubscriptionPlan)
                    ? null
                    : Input.SubscriptionPlan.Trim()),
            ct);

        if (!result.IsSuccess || result.Value is null)
        {
            var redirect = await HandleFailureAsync(result);
            if (redirect is not null)
            {
                return redirect;
            }

            return Page();
        }

        SuccessMessage = $"{result.Value.Name} was created. Add its first administrator so "
                         + "someone can sign in.";

        return RedirectToPage("/Platform/Tenants/Detail", new { id = result.Value.Id });
    }
}
