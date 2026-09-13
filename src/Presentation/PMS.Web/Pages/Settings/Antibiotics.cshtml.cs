using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Settings;

/// <summary>
/// The antibiotic prescription mode. Admin only.
///
/// <para>A small page with unusually long labels, and that is the point: this is a setting with
/// legal consequences in both directions. Tightening it stops Employees dispensing mid-shift;
/// loosening it stops prescriptions being recorded at all. Somebody choosing between the three
/// should not have to infer what they do from three one-word names.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class AntibioticsModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public AntibioticsModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty]
    public AntibioticPrescriptionMode Mode { get; set; }

    /// <summary>What the pharmacy is on now, for the "no change" case and the confirmation.</summary>
    public AntibioticPrescriptionMode CurrentMode { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetAntibioticModeAsync(ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        CurrentMode = (result.Value ?? AntibioticMode.Default).Mode;
        Mode = CurrentMode;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var result = await _api.SetAntibioticModeAsync(Mode, ct);

        if (!result.IsSuccess)
        {
            var redirect = await HandleFailureAsync(result);

            if (redirect is not null)
            {
                return redirect;
            }

            // Reload what the pharmacy is actually on, so a failed save does not leave the radios
            // showing a mode that was never applied.
            var current = await _api.GetAntibioticModeAsync(ct);
            CurrentMode = (current.Value ?? AntibioticMode.Default).Mode;

            return Page();
        }

        var saved = (result.Value ?? AntibioticMode.Default).Mode;

        SuccessMessage = saved switch
        {
            AntibioticPrescriptionMode.Off =>
                "Prescription capture is off. Antibiotic sales are still recorded in the "
                + "register.",
            AntibioticPrescriptionMode.Optional =>
                "Prescription capture is optional. Staff can record details when they have "
                + "them, and no sale will be blocked.",
            _ =>
                "Prescription capture is required. Employees can no longer sell antibiotics, "
                + "and every antibiotic sale now needs a verified prescription. Let your staff "
                + "know.",
        };

        return RedirectToPage();
    }
}
