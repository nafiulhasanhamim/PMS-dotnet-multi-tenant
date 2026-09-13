using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.ViewModels;

namespace PMS.Web.Pages.Salary.Entries;

/// <summary>
/// Corrects a generated salary that has not been paid yet.
///
/// <para><b>Refuses outright once the entry is paid</b>, rather than rendering a form the API
/// would reject. The money has gone and the employee has a slip; a correction after payment is a
/// deliberate database intervention, documented in the module doc, not a screen.</para>
///
/// <para>The base salary is not editable here at all. It is what the employee earned that month;
/// changing it would be inventing a different past.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class EditModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public EditModel(PmsApiClient api) => _api = api;

    [BindProperty(SupportsGet = true, Name = "id")]
    public Guid EntryId { get; set; }

    [BindProperty]
    public EntryInput Input { get; set; } = new();

    public SalaryEntryRow? Entry { get; private set; }

    public string PeriodName => Entry is null
        ? "this month"
        : new DateOnly(Entry.Year, Entry.Month, 1).ToString("MMMM yyyy");

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var loaded = await LoadAsync(ct);

        if (loaded is not null)
        {
            return loaded;
        }

        Input = new EntryInput
        {
            Bonus = Entry!.Bonus,
            AdvanceDeduction = Entry.AdvanceDeduction,
            OtherDeduction = Entry.OtherDeduction,
            AdjustmentNotes = Entry.AdjustmentNotes,
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var loaded = await LoadAsync(ct);

        if (loaded is not null)
        {
            return loaded;
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _api.UpdateSalaryEntryAsync(
            EntryId,
            Input.Bonus ?? 0m,
            Input.AdvanceDeduction ?? 0m,
            Input.OtherDeduction ?? 0m,
            Input.AdjustmentNotes,
            ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        var saved = result.Value!;

        // The deduction the server actually applied, which can be less than what was asked for
        // when the month could not cover it. Reporting the request back would be a lie.
        SuccessMessage =
            $"{saved.EmployeeName}'s salary for {PeriodName} updated. "
            + $"Net payable is now {Money.Format(saved.NetPayable)}"
            + (saved.AdvanceDeduction != (Input.AdvanceDeduction ?? 0m)
                ? $", with {Money.Format(saved.AdvanceDeduction)} of advances recovered - the "
                    + "rest could not be covered this month and carries over."
                : ".");

        return RedirectToPage("/Salary/Entries/Index");
    }

    private async Task<IActionResult?> LoadAsync(CancellationToken ct)
    {
        var result = await _api.GetSalaryEntryAsync(EntryId, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? NotFound();
        }

        Entry = result.Value!;

        if (!Entry.CanEdit)
        {
            // Not a form with a disabled button. A paid entry has no editable state at all, and
            // the slip is what somebody following an old link actually wants.
            return RedirectToPage("/Salary/Entries/Slip", new { id = EntryId });
        }

        return null;
    }

    public sealed class EntryInput
    {
        [Range(0, 99999999, ErrorMessage = "A bonus cannot be negative.")]
        public decimal? Bonus { get; set; }

        [Range(0, 99999999, ErrorMessage = "An advance deduction cannot be negative.")]
        public decimal? AdvanceDeduction { get; set; }

        [Range(0, 99999999, ErrorMessage = "A deduction cannot be negative.")]
        public decimal? OtherDeduction { get; set; }

        [StringLength(1000)]
        public string? AdjustmentNotes { get; set; }
    }
}
