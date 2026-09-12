using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.ViewModels;

namespace PMS.Web.Pages.Salary.Advances;

/// <summary>
/// Records cash handed to an employee mid-month.
///
/// <para><b>A screen of its own, and that is the whole design.</b> An advance typed in as a
/// number during salary generation depends on somebody recalling a note handed over three weeks
/// earlier - and when they do not, the employee is paid in full on top of money they have already
/// had. Recording it here, when it happens, also puts the expense in the month the cash actually
/// left the register.</para>
///
/// <para>Every active profile's outstanding total is loaded with the form, so the page can show
/// "already outstanding" the moment somebody is chosen without another round-trip. Somebody about
/// to hand over more cash should see what is already owed.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class CreateModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public CreateModel(PmsApiClient api) => _api = api;

    [BindProperty]
    public AdvanceInput Input { get; set; } = new();

    public IReadOnlyList<SalaryProfileOption> Employees { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Input.AdvanceDate = DateOnly.FromDateTime(DateTime.UtcNow);

        return await LoadEmployeesAsync(ct) ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return await LoadEmployeesAsync(ct) ?? Page();
        }

        var result = await _api.RecordSalaryAdvanceAsync(
            Input.ProfileId!.Value, Input.Amount!.Value, Input.AdvanceDate, Input.Reason, ct);

        if (!result.IsSuccess)
        {
            var redirect = await HandleFailureAsync(result);

            return redirect ?? await LoadEmployeesAsync(ct) ?? Page();
        }

        var recorded = result.Value!;

        // The outstanding total after, not just the amount recorded. Somebody handing over a
        // third advance should see the running figure without opening another screen.
        SuccessMessage =
            $"{Money.Format(recorded.Amount)} advance recorded for {recorded.EmployeeName}. "
            + $"They now have {Money.Format(recorded.UnsettledTotalAfter)} outstanding.";

        return RedirectToPage("/Salary/Advances/Index");
    }

    private async Task<IActionResult?> LoadEmployeesAsync(CancellationToken ct)
    {
        var result = await _api.GetSalaryProfileOptionsAsync(ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result);
        }

        Employees = result.Value ?? [];

        return null;
    }

    public sealed class AdvanceInput
    {
        [Required(ErrorMessage = "Choose an employee.")]
        public Guid? ProfileId { get; set; }

        [Required(ErrorMessage = "Enter the amount handed over.")]
        [Range(0.01, 99999999, ErrorMessage = "Enter an amount greater than zero.")]
        public decimal? Amount { get; set; }

        [DataType(DataType.Date)]
        public DateOnly? AdvanceDate { get; set; }

        [StringLength(500)]
        public string? Reason { get; set; }
    }
}
