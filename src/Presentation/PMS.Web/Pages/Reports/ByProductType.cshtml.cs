using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Reports;

/// <summary>
/// Revenue and margin split by product type.
///
/// <para>Worth having only since pharmacies started stocking non-medicines: it answers "how much
/// of our margin comes from medicines versus baby care", which a single blended margin cannot.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class ByProductTypeModel : ReportRangePageModel
{
    private readonly PmsApiClient _api;

    public ByProductTypeModel(PmsApiClient api) => _api = api;

    public IReadOnlyList<SalesByProductTypeRow> Rows { get; private set; } = [];

    public decimal TotalRevenue => Rows.Sum(r => r.Revenue);

    public decimal TotalCost => Rows.Sum(r => r.Cost);

    public decimal TotalProfit => Rows.Sum(r => r.Profit);

    public decimal TotalMargin =>
        TotalRevenue == 0m ? 0m : TotalProfit / TotalRevenue * 100m;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Prepare();

        var result = await _api.GetSalesByProductTypeAsync(From, To, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Rows = result.Value ?? [];

        return Page();
    }

    public Task<IActionResult> OnGetExportAsync(CancellationToken ct) =>
        ExportAsync(
            _api,
            "api/reports/sales-by-product-type/export"
                + PmsApiClient.RangeQuery(From, To, "?"),
            "sales-by-product-type.csv",
            ct);
}
