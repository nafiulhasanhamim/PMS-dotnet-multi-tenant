using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Antibiotics;

/// <summary>
/// The antibiotic sales register.
///
/// <para><b>Admin and Pharmacist.</b> An Employee may be able to sell an antibiotic — that now
/// depends on the pharmacy's mode — but never to read this. Selling is counter work; the register
/// is the regulatory record of what was dispensed and to whom.</para>
///
/// <para>This page may be printed and handed to an inspector, which drives most of its design:
/// see the frontend doc.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantWriter)]
public class RegisterModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public RegisterModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true, Name = "from")]
    public DateOnly? From { get; set; }

    [BindProperty(SupportsGet = true, Name = "to")]
    public DateOnly? To { get; set; }

    [BindProperty(SupportsGet = true, Name = "product")]
    public Guid? ProductId { get; set; }

    [BindProperty(SupportsGet = true, Name = "doctor")]
    public string? DoctorName { get; set; }

    [BindProperty(SupportsGet = true, Name = "cashier")]
    public Guid? CashierUserId { get; set; }

    [BindProperty(SupportsGet = true, Name = "rx")]
    public PrescriptionStatusFilter PrescriptionStatus { get; set; }
        = PrescriptionStatusFilter.All;

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public AntibioticRegisterPage Register { get; private set; } = AntibioticRegisterPage.Empty;

    /// <summary>The pharmacy name, for the printed header. Read from the session, not the API.</summary>
    public string PharmacyName { get; private set; } = "Pharmacy";

    /// <summary>Only an Admin can act on the mode, so only an Admin gets the settings link.</summary>
    public bool CanChangeMode { get; private set; }

    public bool HasFilters =>
        ProductId is not null
        || !string.IsNullOrWhiteSpace(DoctorName)
        || CashierUserId is not null
        || PrescriptionStatus != PrescriptionStatusFilter.All;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        PharmacyName = User.TenantName() ?? "Pharmacy";
        CanChangeMode = User.IsTenantAdmin();

        var result = await _api.GetAntibioticRegisterAsync(
            From, To, ProductId, DoctorName, CashierUserId, PrescriptionStatus,
            PageNumber, pageSize: 25, ct: ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Register = result.Value ?? AntibioticRegisterPage.Empty;

        return Page();
    }

    /// <summary>
    /// The CSV, passed through from the API.
    ///
    /// <para>Streamed rather than read into memory here: the API streams it and buffering it in
    /// this app on its way to a file the browser is already writing would undo that. The filters
    /// are the same ones the page is showing, so the export covers exactly what is on screen —
    /// not one page of it.</para>
    /// </summary>
    public async Task<IActionResult> OnGetExportAsync(CancellationToken ct)
    {
        var result = await _api.ExportAntibioticRegisterAsync(
            From, To, ProductId, DoctorName, CashierUserId, PrescriptionStatus, ct);

        if (!result.IsSuccess)
        {
            var redirect = await HandleFailureAsync(result);

            if (redirect is not null)
            {
                return redirect;
            }

            // Back to the register with the message rather than a bare error page: the person
            // still wants the register, they just did not get the file.
            TempData["ExportFailed"] = result.ErrorMessage;

            return RedirectToPage(RouteValues);
        }

        var file = result.Value!;

        return File(file.Content, file.ContentType, file.FileName);
    }

    public Dictionary<string, string> RouteValues => new()
    {
        ["from"] = From?.ToString("yyyy-MM-dd") ?? string.Empty,
        ["to"] = To?.ToString("yyyy-MM-dd") ?? string.Empty,
        ["product"] = ProductId?.ToString() ?? string.Empty,
        ["doctor"] = DoctorName ?? string.Empty,
        ["cashier"] = CashierUserId?.ToString() ?? string.Empty,
        ["rx"] = PrescriptionStatus.ToString(),
    };
}
