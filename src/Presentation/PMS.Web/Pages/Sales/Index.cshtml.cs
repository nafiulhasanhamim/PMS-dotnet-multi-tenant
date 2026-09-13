using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.ViewModels;

namespace PMS.Web.Pages.Sales;

/// <summary>
/// Every sale, newest first.
///
/// <para><b>An Employee sees only their own, and this page does not decide that.</b> The API
/// applies the restriction inside the query, so the row count and the page count are theirs
/// too. What the page does is not offer them a cashier filter — a control whose only options
/// are "me" and other people's names, most of which return nothing, is a worse experience than
/// no control.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantUser)]
public class IndexModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public IndexModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true, Name = "from")]
    public DateOnly? From { get; set; }

    [BindProperty(SupportsGet = true, Name = "to")]
    public DateOnly? To { get; set; }

    [BindProperty(SupportsGet = true, Name = "cashier")]
    public Guid? Cashier { get; set; }

    [BindProperty(SupportsGet = true, Name = "status")]
    public SaleStatusFilter Status { get; set; } = SaleStatusFilter.All;

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public ApiPage<SaleListItem> Sales { get; private set; } = ApiPage<SaleListItem>.Empty;

    /// <summary>Empty for an Employee, which is how the page knows not to render the filter.</summary>
    public IReadOnlyList<CashierOption> Cashiers { get; private set; } = [];

    public BillingLimits Limits { get; private set; } = BillingLimits.None;

    public bool HasFilters =>
        From is not null || To is not null || Cashier is not null
        || Status != SaleStatusFilter.All;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetSalesAsync(
            From, To, Cashier, Status, PageNumber, pageSize: 25, ct: ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Sales = result.Value ?? ApiPage<SaleListItem>.Empty;

        // Both of these are conveniences and neither is a control: the API refuses a cancel
        // from a Pharmacist whatever this page renders.
        var cashiers = await _api.GetCashiersAsync(ct);
        Cashiers = cashiers.Value ?? [];

        var limits = await _api.GetBillingLimitsAsync(ct);
        Limits = limits.Value ?? BillingLimits.None;

        return Page();
    }

    /// <summary>
    /// Cancels a sale. Admin only, enforced by the API — the confirm modal on this page is
    /// there so somebody cannot do it by reflex, not to decide who may.
    /// </summary>
    public async Task<IActionResult> OnPostCancelAsync(
        Guid id, string reason, CancellationToken ct)
    {
        var result = await _api.CancelSaleAsync(id, reason ?? string.Empty, ct);

        if (!result.IsSuccess)
        {
            var redirect = await HandleFailureAsync(result);

            if (redirect is not null)
            {
                return redirect;
            }

            // Reload the list underneath the error rather than showing a bare banner on an
            // empty page.
            return await OnGetAsync(ct);
        }

        var cancelled = result.Value!;

        SuccessMessage =
            $"Invoice {cancelled.InvoiceNumber} cancelled. "
            + $"{cancelled.RestoredInBaseUnits} units restored across "
            + $"{Copy.Count(cancelled.LinesRestored, "line")}."
            + (cancelled.SkippedAlreadyReturned > 0
                ? $" {Copy.Count(cancelled.SkippedAlreadyReturned, "line")} had already been "
                  + "returned in part or in full, so that stock was not restored twice."
                : string.Empty);

        return RedirectToPage(new { from = From, to = To, cashier = Cashier, status = Status, p = PageNumber });
    }

    public Dictionary<string, string> RouteValues => new()
    {
        ["from"] = From?.ToString("yyyy-MM-dd") ?? string.Empty,
        ["to"] = To?.ToString("yyyy-MM-dd") ?? string.Empty,
        ["cashier"] = Cashier?.ToString() ?? string.Empty,
        ["status"] = Status.ToString(),
    };
}
