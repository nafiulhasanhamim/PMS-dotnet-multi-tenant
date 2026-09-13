using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.ViewModels;

namespace PMS.Web.Pages;

/// <summary>
/// The home screen.
///
/// <para><b>One API call.</b> Until Module 10 this page made three — the profile, the alert
/// summary and the antibiotic figure — and every new module would have added another. The server
/// now assembles the whole screen, which also means role visibility is decided in one place
/// rather than re-derived here from cookie claims.</para>
///
/// <para><b>The greeting name comes from the cookie, not the API.</b> The API's token carries a
/// subject, a tenant and a role but no name, so asking it for one would mean a database round
/// trip to say "Welcome, Karim". The sign-in cookie already has it.</para>
///
/// <para><b>A failure here does not take the page down.</b> The quick actions are the most
/// valuable thing on this screen for an Employee, and a cashier should not lose their way to the
/// till because a count could not be fetched.</para>
/// </summary>
public class IndexModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public IndexModel(PmsApiClient api)
    {
        _api = api;
    }

    public Dashboard? Data { get; private set; }

    /// <summary>The four alert cards, or empty when the dashboard could not be loaded.</summary>
    public IReadOnlyList<AlertCardModel> Cards { get; private set; } = [];

    /// <summary>True when the dashboard call failed. The page still renders — see the remarks.</summary>
    public bool Unavailable { get; private set; }

    /// <summary>
    /// What this pharmacy is called. From settings when the call succeeded, and from the sign-in
    /// cookie when it did not — a home screen that could not name the pharmacy would be a poor
    /// thing to show somebody with access to two of them.
    /// </summary>
    public string PharmacyName { get; private set; } = string.Empty;

    public string UserName { get; private set; } = string.Empty;

    public UserRole? Role { get; private set; }

    /// <summary>
    /// Whether to offer the stock and purchase quick actions. An Employee sells; they do not book
    /// deliveries in, and offering them a button that 403s would be a worse introduction to the
    /// system than not offering it.
    /// </summary>
    public bool MayAddStock => Role is UserRole.Admin or UserRole.Pharmacist;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        UserName = User.FullName() ?? "there";
        Role = User.Role();
        PharmacyName = User.TenantName() ?? "your pharmacy";

        var result = await _api.GetDashboardAsync(ct);

        if (!result.IsSuccess)
        {
            // A 401 or a suspended tenant still redirects — those mean the session itself is no
            // longer usable, and rendering a dashboard over a dead session helps nobody.
            var redirect = await HandleFailureAsync(result);

            if (redirect is not null)
            {
                return redirect;
            }

            Unavailable = true;

            return Page();
        }

        Data = result.Value;

        if (Data is not null)
        {
            Cards = AlertCardModel.From(Data.Alerts);

            // The server's answer wins: it comes from settings, where the cookie's copy is
            // whatever the tenant was called when this person signed in.
            PharmacyName = string.IsNullOrWhiteSpace(Data.PharmacyName)
                ? PharmacyName
                : Data.PharmacyName;

            // And the server's view of the role, for the same reason — it decided which cards
            // exist, so it should decide which quick actions sit beside them.
            Role = Data.Role;
        }

        return Page();
    }
}
