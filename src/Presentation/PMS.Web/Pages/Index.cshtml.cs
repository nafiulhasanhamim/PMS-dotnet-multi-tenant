using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.ViewModels;

namespace PMS.Web.Pages;

public class IndexModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public IndexModel(PmsApiClient api)
    {
        _api = api;
    }

    public MyProfile? Profile { get; private set; }

    public AlertSummary Summary { get; private set; } = AlertSummary.Empty;

    /// <summary>
    /// Antibiotics dispensed this month. Admin and Pharmacist only — an Employee does not see the
    /// register, so a card summarising it would be a number they cannot open.
    /// </summary>
    public AntibioticMonthlySummary? Antibiotics { get; private set; }

    public IReadOnlyList<AlertCardModel> Cards { get; private set; } = [];

    /// <summary>
    /// True when the alert lookup failed. The rest of the dashboard still renders — see below.
    /// </summary>
    public bool AlertsUnavailable { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        // Reading the profile from the API on every load is the point of this page in Module
        // 1: it proves the cookie session, the stored token and the tenant context all work
        // together. It is also a live check - rendering the cookie's own claims would keep
        // showing a dashboard for a pharmacy that had been suspended an hour ago.
        var result = await _api.GetMyProfileAsync(ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Profile = result.Value;

        // Module 6. A failure here does NOT take the page down: the alert panels are one part
        // of a dashboard, and a pharmacy should not lose its home screen because a count could
        // not be fetched. The cards are replaced by a line saying so.
        var alerts = await _api.GetAlertSummaryAsync(ct);

        if (alerts.IsSuccess)
        {
            Summary = alerts.Value ?? AlertSummary.Empty;
            Cards = AlertCardModel.From(Summary);
        }
        else
        {
            AlertsUnavailable = true;
        }

        // Module 7. Informational rather than an alert: a pharmacy dispensing antibiotics is a
        // pharmacy doing its job, and colouring the figure as a warning would imply otherwise.
        // Fetched only for the roles that can act on it.
        if (User.IsTenantAdmin() || User.Role() == UserRole.Pharmacist)
        {
            var antibiotics = await _api.GetAntibioticMonthlySummaryAsync(ct: ct);

            // A failure here is silent for the same reason the alert panels tolerate one: a
            // dashboard should not fall over because one card could not be filled in.
            if (antibiotics.IsSuccess)
            {
                Antibiotics = antibiotics.Value;
            }
        }

        return Page();
    }
}
