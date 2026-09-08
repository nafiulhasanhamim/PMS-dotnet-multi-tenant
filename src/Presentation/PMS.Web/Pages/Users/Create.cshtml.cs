using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.ViewModels;

namespace PMS.Web.Pages.Users;

[Authorize(Policy = WebPolicies.TenantAdmin)]
public class CreateModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public CreateModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>
    /// Staff, not peers — exactly what the API accepts here.
    ///
    /// Admin is absent because a pharmacy Admin cannot create another Admin: further Admins
    /// come from a platform operator. PlatformAdmin is absent because it cannot exist against
    /// a pharmacy at all, and the database check constraint would refuse the row. Offering
    /// either would mean showing a choice that comes back as a 400.
    /// </summary>
    public static IReadOnlyList<UserRole> AssignableRoles { get; } = new[]
    {
        UserRole.Pharmacist,
        UserRole.Employee,
    };

    public string TenantName => User.TenantName() ?? "this pharmacy";

    public sealed class InputModel
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Employee;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
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

        if (!AssignableRoles.Contains(Input.Role))
        {
            ModelState.AddModelError("Input.Role", "Choose Pharmacist or Employee.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _api.CreateUserAsync(
            new CreateTenantUserRequest(
                Input.Email.Trim(), Input.FullName.Trim(), Input.Password, Input.Role),
            ct);

        if (!result.IsSuccess || result.Value is null)
        {
            var redirect = await HandleFailureAsync(result);
            return redirect ?? Page();
        }

        var roleText = StatusPresentation.ForRole(result.Value.Role).Text;

        // These two messages must not read alike. In the linked case the password the Admin
        // just typed was NOT applied, and an Admin who tells a new colleague that password
        // creates a support call and a person who cannot sign in. So the message says it
        // outright rather than hinting.
        SuccessMessage = result.Value.Outcome == ProvisioningOutcome.ExistingUserLinked
            ? $"{result.Value.Email} already had a PMS account, and now has {roleText} access "
              + $"to {TenantName}. Important: the password you entered was not applied — they "
              + "sign in with the password they already use."
            : $"{result.Value.Email} was created as {roleText} at {TenantName}. They can sign "
              + "in with the password you entered.";

        return RedirectToPage("/Users/Index");
    }
}
