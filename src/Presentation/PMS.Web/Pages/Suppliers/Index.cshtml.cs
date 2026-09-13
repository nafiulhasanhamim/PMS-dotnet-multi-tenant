using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Suppliers;

/// <summary>
/// The suppliers a pharmacy buys from, with what is still owed to each.
///
/// <para><b>Admin and Pharmacist.</b> An Employee sees neither the nav entry nor the page: what
/// the pharmacy pays for its goods, and to whom it owes money, is not counter information.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantWriter)]
public class IndexModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public IndexModel(PmsApiClient api) => _api = api;

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "status")]
    public SupplierStatusFilter Status { get; set; } = SupplierStatusFilter.Active;

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public ApiPage<SupplierListItem> Suppliers { get; private set; }
        = ApiPage<SupplierListItem>.Empty;

    /// <summary>Only an Admin may deactivate, so only an Admin sees the status filter act.</summary>
    public bool IsAdmin { get; private set; }

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Search) || Status != SupplierStatusFilter.Active;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        IsAdmin = User.IsTenantAdmin();

        var result = await _api.GetSuppliersAsync(Search, Status, PageNumber, 25, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Suppliers = result.Value ?? ApiPage<SupplierListItem>.Empty;

        return Page();
    }

    public Dictionary<string, string> RouteValues => new()
    {
        ["q"] = Search ?? string.Empty,
        ["status"] = Status.ToString(),
    };
}
