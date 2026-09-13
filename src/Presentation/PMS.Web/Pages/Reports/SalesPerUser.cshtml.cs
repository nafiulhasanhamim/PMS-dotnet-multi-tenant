using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Reports;

/// <summary>
/// Per cashier: what they rang up and what they discounted.
///
/// <para>The page says in as many words that a high discount figure is a question rather than an
/// accusation. A screen that ranks colleagues invites a conclusion, and the honest reading — that
/// one counter may serve the regulars, or the elderly, or the shift when the manager authorises
/// goodwill — is worth putting in front of whoever opens it.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class SalesPerUserModel : ReportRangePageModel
{
    private readonly PmsApiClient _api;

    public SalesPerUserModel(PmsApiClient api) => _api = api;

    public IReadOnlyList<SalesPerUserRow> Rows { get; private set; } = [];

    public decimal TotalSales => Rows.Sum(r => r.TotalSales);

    public int TotalTransactions => Rows.Sum(r => r.TransactionCount);

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Prepare();

        var result = await _api.GetSalesPerUserAsync(From, To, ct);

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
            "api/reports/sales-per-user/export" + PmsApiClient.RangeQuery(From, To, "?"),
            "sales-per-user.csv",
            ct);
}
