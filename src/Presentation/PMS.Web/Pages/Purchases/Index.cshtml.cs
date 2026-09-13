using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Purchases;

/// <summary>Deliveries recorded against a supplier's bill.</summary>
[Authorize(Policy = WebPolicies.TenantWriter)]
public class IndexModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public IndexModel(PmsApiClient api) => _api = api;

    [BindProperty(SupportsGet = true, Name = "supplier")]
    public Guid? SupplierId { get; set; }

    [BindProperty(SupportsGet = true, Name = "from")]
    public DateOnly? From { get; set; }

    [BindProperty(SupportsGet = true, Name = "to")]
    public DateOnly? To { get; set; }

    [BindProperty(SupportsGet = true, Name = "status")]
    public PurchaseStatusFilter Status { get; set; } = PurchaseStatusFilter.All;

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public ApiPage<PurchaseListItem> Purchases { get; private set; }
        = ApiPage<PurchaseListItem>.Empty;

    public IReadOnlyList<SupplierOption> Suppliers { get; private set; } = [];

    public bool HasFilters =>
        SupplierId is not null || From is not null || To is not null
        || Status != PurchaseStatusFilter.All;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var options = await _api.GetSupplierOptionsAsync(ct);

        if (options.IsSuccess)
        {
            Suppliers = options.Value ?? [];
        }

        var result = await _api.GetPurchasesAsync(
            SupplierId, From, To, Status, PageNumber, 25, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Purchases = result.Value ?? ApiPage<PurchaseListItem>.Empty;

        return Page();
    }

    public Dictionary<string, string> RouteValues => new()
    {
        ["supplier"] = SupplierId?.ToString() ?? string.Empty,
        ["from"] = From?.ToString("yyyy-MM-dd") ?? string.Empty,
        ["to"] = To?.ToString("yyyy-MM-dd") ?? string.Empty,
        ["status"] = Status.ToString(),
    };
}
