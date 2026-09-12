using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.ViewModels;

namespace PMS.Web.Pages.Salary;

/// <summary>
/// A month's payroll in one pass.
///
/// <para><b>The unsettled-advance breakdown is the point of this screen.</b> Each row lists every
/// advance the employee has outstanding - date, amount and reason - rather than a single number to
/// be taken on trust. That list is what replaces somebody trying to remember what they handed over
/// three weeks ago, which is the failure this whole module exists to prevent.</para>
///
/// <para>Profiles already generated for the month appear greyed and unselectable rather than being
/// dropped. An employee missing from the list is indistinguishable from an employee nobody put on
/// the payroll.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class GenerateModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public GenerateModel(PmsApiClient api) => _api = api;

    [BindProperty(SupportsGet = true, Name = "month")]
    public int Month { get; set; }

    [BindProperty(SupportsGet = true, Name = "year")]
    public int Year { get; set; }

    [BindProperty]
    public List<RowInput> Rows { get; set; } = [];

    public SalaryGenerationPreview Preview { get; private set; } = SalaryGenerationPreview.Empty;

    public string PeriodName =>
        Month is >= 1 and <= 12 ? new DateOnly(Year, Month, 1).ToString("MMMM yyyy") : "this month";

    public IReadOnlyList<int> YearOptions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        ApplyDefaultPeriod();

        return await LoadAsync(ct) ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        ApplyDefaultPeriod();

        // Only the ticked rows, and only the ones the server would still accept. An unticked row
        // still posts its inputs - the browser sends every field in the form - so filtering here
        // is what makes the checkbox mean anything.
        var lines = Rows
            .Where(r => r.Selected && r.ProfileId != Guid.Empty)
            .Select(r => new SalaryGenerationLine(
                r.ProfileId,
                r.Bonus ?? 0m,
                r.AdvanceDeduction ?? 0m,
                r.OtherDeduction ?? 0m,
                r.AdjustmentNotes))
            .ToList();

        if (lines.Count == 0)
        {
            ShowError("Select at least one employee to generate salary for.");

            return await LoadAsync(ct) ?? Page();
        }

        var result = await _api.GenerateSalaryAsync(Month, Year, lines, ct);

        if (!result.IsSuccess)
        {
            var redirect = await HandleFailureAsync(result);

            return redirect ?? await LoadAsync(ct) ?? Page();
        }

        var generated = result.Value!;

        // What was actually settled, not what was asked for. The two differ whenever a month
        // could not cover somebody's advances, and saying so here is how an owner finds out
        // without opening the advances list.
        var message =
            $"Salary generated for {generated.EntriesCreated} "
            + $"{(generated.EntriesCreated == 1 ? "employee" : "employees")} for {PeriodName}. "
            + $"Total payable {Money.Format(generated.TotalNetPayable)}.";

        if (generated.AdvancesSettled > 0m)
        {
            message += $" {Money.Format(generated.AdvancesSettled)} of advances recovered.";
        }

        if (generated.AdvancesCarriedOver > 0m)
        {
            message += $" {Money.Format(generated.AdvancesCarriedOver)} of advances could not be "
                + "covered this month and carries over.";
        }

        SuccessMessage = message;

        return RedirectToPage("/Salary/Entries/Index", new { month = Month, year = Year });
    }

    private void ApplyDefaultPeriod()
    {
        var today = DateTime.UtcNow;

        if (Month is < 1 or > 12)
        {
            Month = today.Month;
        }

        if (Year is < 2000 or > 2200)
        {
            Year = today.Year;
        }

        // A window around now rather than every year the constraint allows. Payroll is generated
        // for the month just ended or, occasionally, one that was missed.
        YearOptions = Enumerable.Range(today.Year - 3, 5).ToList();
    }

    private async Task<IActionResult?> LoadAsync(CancellationToken ct)
    {
        var result = await _api.GetSalaryGenerationPreviewAsync(Month, Year, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result);
        }

        Preview = result.Value ?? SalaryGenerationPreview.Empty;

        // Rebuilt from the preview on every load, including after a failed post: a row whose
        // profile has since been deactivated must not survive as a hidden input.
        Rows = Preview.Rows
            .Where(r => !r.AlreadyGenerated)
            .Select(r =>
            {
                var posted = Rows.FirstOrDefault(x => x.ProfileId == r.ProfileId);

                return posted ?? new RowInput
                {
                    ProfileId = r.ProfileId,
                    Selected = true,
                    Bonus = 0m,
                    // Pre-filled with everything outstanding. Editable, because partial
                    // settlement is a real decision an owner makes.
                    AdvanceDeduction = r.UnsettledAdvanceTotal,
                    OtherDeduction = 0m,
                };
            })
            .ToList();

        return null;
    }

    /// <summary>Where a row sits in <see cref="Rows"/>, so model binding round-trips it.</summary>
    public int IndexOf(Guid profileId) => Rows.FindIndex(r => r.ProfileId == profileId);

    public RowInput RowFor(Guid profileId) =>
        Rows.FirstOrDefault(r => r.ProfileId == profileId) ?? new RowInput { ProfileId = profileId };

    public sealed class RowInput
    {
        public Guid ProfileId { get; set; }

        public bool Selected { get; set; }

        [Range(0, 99999999)]
        public decimal? Bonus { get; set; }

        [Range(0, 99999999)]
        public decimal? AdvanceDeduction { get; set; }

        [Range(0, 99999999)]
        public decimal? OtherDeduction { get; set; }

        [StringLength(1000)]
        public string? AdjustmentNotes { get; set; }
    }
}
