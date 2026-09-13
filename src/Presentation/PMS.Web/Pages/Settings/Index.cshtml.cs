using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Settings;

/// <summary>
/// Everything about this pharmacy that an owner can change.
///
/// <para><b>Admin only.</b> Reading settings is open to every role — a till needs the discount
/// caps, an invoice needs the pharmacy's phone number — but the values on this page decide what
/// staff may sell and what they may discount, which is an owner's decision. The API enforces the
/// same split; this attribute only saves a wasted round trip.</para>
///
/// <para><b>One Save button for the whole page.</b> Per-field saves would mean nine forms, nine
/// success banners and nine chances to leave the page half-changed. The API applies the whole set
/// in one transaction or none of it, and this screen is shaped to match: fill the form in, press
/// Save once, and either everything took or nothing did with the reasons shown against the
/// fields.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class IndexModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public IndexModel(PmsApiClient api) => _api = api;

    [BindProperty]
    public SettingsInput Input { get; set; } = new();

    /// <summary>What is currently stored, for the "Current" badges on the mode radios.</summary>
    public TenantSettings Current { get; private set; } = TenantSettings.Fallback;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var loaded = await LoadAsync(ct);

        if (loaded is not null)
        {
            return loaded;
        }

        Input = new SettingsInput
        {
            PharmacyName = Current.PharmacyName,
            PharmacyAddress = Current.PharmacyAddress,
            PharmacyPhone = Current.PharmacyPhone,
            PharmacyLicenseNumber = Current.PharmacyLicenseNumber,
            ExpiryAlertWindowDays = Current.ExpiryAlertWindowDays,
            DeadStockThresholdDays = Current.DeadStockThresholdDays,
            DefaultReorderLevel = Current.DefaultReorderLevel,
            DiscountCapEmployeePercent = Current.DiscountCapEmployeePercent,
            DiscountCapPharmacistPercent = Current.DiscountCapPharmacistPercent,
            AntibioticPrescriptionMode = Current.AntibioticPrescriptionMode,
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        // Reloaded either way, so a failed save still renders the "Current" badges against what
        // is really stored rather than against what the person typed.
        var loaded = await LoadAsync(ct);

        if (loaded is not null)
        {
            return loaded;
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // The whole set, every time. The API treats a null field as "leave it alone", and this
        // page has every field in front of the person, so sending all of them is honest: what
        // they see is what will be stored.
        var result = await _api.UpdateSettingsAsync(new
        {
            pharmacyName = Input.PharmacyName,
            pharmacyAddress = Input.PharmacyAddress ?? string.Empty,
            pharmacyPhone = Input.PharmacyPhone,
            pharmacyLicenseNumber = Input.PharmacyLicenseNumber ?? string.Empty,
            expiryAlertWindowDays = Input.ExpiryAlertWindowDays,
            deadStockThresholdDays = Input.DeadStockThresholdDays,
            defaultReorderLevel = Input.DefaultReorderLevel,
            discountCapEmployeePercent = Input.DiscountCapEmployeePercent,
            discountCapPharmacistPercent = Input.DiscountCapPharmacistPercent,
            antibioticPrescriptionMode = (int)Input.AntibioticPrescriptionMode,
        }, ct);

        if (!result.IsSuccess)
        {
            // The API is the authoritative validator: its per-field messages land under the same
            // inputs the client-side hints would have flagged.
            return await HandleFailureAsync(result) ?? Page();
        }

        var saved = result.Value!;

        SuccessMessage = saved.AnythingChanged
            ? $"Settings saved. {Describe(saved.ChangedKeys.Count)} in force from now on."
            : "Settings saved. Nothing had changed.";

        return RedirectToPage("/Settings/Index");
    }

    private async Task<IActionResult?> LoadAsync(CancellationToken ct)
    {
        var result = await _api.GetSettingsAsync(ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result);
        }

        Current = result.Value ?? TenantSettings.Fallback;

        return null;
    }

    private static string Describe(int count) =>
        count == 1 ? "One change is" : $"{count} changes are";

    /// <summary>
    /// <para>Client-side rules mirror the API's exactly. They exist to catch a typo before a
    /// round trip, not to be the control — the server refuses the same values with the same
    /// bounds, and a browser with scripting off is refused there instead.</para>
    /// </summary>
    public sealed class SettingsInput
    {
        [Required(ErrorMessage = "The pharmacy needs a name - it appears on every invoice.")]
        [StringLength(200)]
        public string? PharmacyName { get; set; }

        [StringLength(500)]
        public string? PharmacyAddress { get; set; }

        [Required(ErrorMessage = "A phone number is required - it appears on every invoice.")]
        [StringLength(100)]
        public string? PharmacyPhone { get; set; }

        [StringLength(100)]
        public string? PharmacyLicenseNumber { get; set; }

        [Required(ErrorMessage = "Enter the expiry alert window in days.")]
        [Range(1, 3650, ErrorMessage = "Enter a number of days between 1 and 3650.")]
        public int? ExpiryAlertWindowDays { get; set; }

        [Required(ErrorMessage = "Enter the dead stock threshold in days.")]
        [Range(1, 3650, ErrorMessage = "Enter a number of days between 1 and 3650.")]
        public int? DeadStockThresholdDays { get; set; }

        [Required(ErrorMessage = "Enter a default reorder level.")]
        [Range(1, 1_000_000, ErrorMessage = "Enter a reorder level greater than zero.")]
        public int? DefaultReorderLevel { get; set; }

        // Zero is allowed, unlike the day thresholds: a pharmacy that lets nobody below Admin
        // discount anything is making a coherent choice.
        [Required(ErrorMessage = "Enter a maximum discount for employees.")]
        [Range(0, 100, ErrorMessage = "Enter a percentage between 0 and 100.")]
        public int? DiscountCapEmployeePercent { get; set; }

        [Required(ErrorMessage = "Enter a maximum discount for pharmacists.")]
        [Range(0, 100, ErrorMessage = "Enter a percentage between 0 and 100.")]
        public int? DiscountCapPharmacistPercent { get; set; }

        public AntibioticPrescriptionMode AntibioticPrescriptionMode { get; set; }
    }
}
