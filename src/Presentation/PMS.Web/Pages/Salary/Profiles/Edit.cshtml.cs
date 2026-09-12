using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Salary.Profiles;

/// <summary>
/// Puts somebody on the payroll, or changes what they earn.
///
/// <para>One page for both, because the form is nearly identical and two would drift. The one
/// difference is the employee: choosing who is a create-time decision, since moving a profile
/// from one person to another would move their salary history with it.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class EditModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public EditModel(PmsApiClient api) => _api = api;

    [BindProperty(SupportsGet = true, Name = "id")]
    public Guid? ProfileId { get; set; }

    [BindProperty]
    public ProfileInput Input { get; set; } = new();

    /// <summary>Users without an active profile. Empty on the edit form, which needs none.</summary>
    public IReadOnlyList<SelectListItem> EligibleUsers { get; private set; } = [];

    public string? EmployeeName { get; private set; }

    public bool IsEdit => ProfileId is not null;

    public string Title => IsEdit ? "Edit salary profile" : "Add salary profile";

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (ProfileId is not { } id)
        {
            Input.JoiningDate = DateOnly.FromDateTime(DateTime.UtcNow);

            return await LoadEligibleAsync(ct) ?? Page();
        }

        var result = await _api.GetSalaryProfileAsync(id, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? NotFound();
        }

        var profile = result.Value!;
        EmployeeName = profile.EmployeeName;

        Input = new ProfileInput
        {
            UserId = profile.UserId,
            Designation = profile.Designation,
            MonthlyBaseSalary = profile.MonthlyBaseSalary,
            JoiningDate = profile.JoiningDate,
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return IsEdit ? await ReloadNameAsync(ct) : await LoadEligibleAsync(ct) ?? Page();
        }

        var result = ProfileId is { } id
            ? await _api.UpdateSalaryProfileAsync(
                id, Input.Designation, Input.MonthlyBaseSalary!.Value, Input.JoiningDate!.Value, ct)
            : await _api.CreateSalaryProfileAsync(
                Input.UserId!.Value, Input.Designation, Input.MonthlyBaseSalary!.Value,
                Input.JoiningDate!.Value, ct);

        if (!result.IsSuccess)
        {
            var redirect = await HandleFailureAsync(result);

            if (redirect is not null)
            {
                return redirect;
            }

            return IsEdit ? await ReloadNameAsync(ct) : await LoadEligibleAsync(ct) ?? Page();
        }

        SuccessMessage = IsEdit
            ? $"{result.Value!.EmployeeName} updated. Salaries already generated are unchanged."
            : $"{result.Value!.EmployeeName} is on the payroll.";

        return RedirectToPage("/Salary/Profiles/Index");
    }

    private async Task<IActionResult?> LoadEligibleAsync(CancellationToken ct)
    {
        var result = await _api.GetSalaryEligibleUsersAsync(ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result);
        }

        EligibleUsers = (result.Value ?? [])
            .Select(u => new SelectListItem(
                $"{u.Name} ({u.Role})", u.UserId.ToString(), u.UserId == Input.UserId))
            .ToList();

        return null;
    }

    private async Task<IActionResult> ReloadNameAsync(CancellationToken ct)
    {
        if (ProfileId is { } id)
        {
            var result = await _api.GetSalaryProfileAsync(id, ct);

            if (result.IsSuccess)
            {
                EmployeeName = result.Value!.EmployeeName;
            }
        }

        return Page();
    }

    public sealed class ProfileInput
    {
        [Required(ErrorMessage = "Choose an employee.")]
        public Guid? UserId { get; set; }

        [StringLength(200)]
        public string? Designation { get; set; }

        // Greater than zero, matching the API. A profile is a statement that somebody is paid;
        // a zero would put a name on the payroll that costs nothing.
        [Required(ErrorMessage = "Enter the monthly salary.")]
        [Range(0.01, 99999999, ErrorMessage = "Enter a monthly salary greater than zero.")]
        public decimal? MonthlyBaseSalary { get; set; }

        [Required(ErrorMessage = "Enter the joining date.")]
        [DataType(DataType.Date)]
        public DateOnly? JoiningDate { get; set; }
    }
}
