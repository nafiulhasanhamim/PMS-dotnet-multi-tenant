using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Stock;

/// <summary>
/// Every change ever made to one batch, and who made it.
///
/// <para><b>Admin or Pharmacist</b>, matching the API. This is the one read in the module an
/// Employee cannot make: it is who wrote off what and why, which is staff-conduct information
/// rather than stock information.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantWriter)]
public class BatchHistoryModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public BatchHistoryModel(PmsApiClient api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true, Name = "id")]
    public Guid BatchId { get; set; }

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public BatchModel? Batch { get; private set; }

    public ApiPage<StockAdjustmentModel> Adjustments { get; private set; }
        = ApiPage<StockAdjustmentModel>.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var batch = await _api.GetBatchAsync(BatchId, ct);

        if (!batch.IsSuccess)
        {
            if (batch.Problem?.Status == StatusCodes.Status404NotFound)
            {
                return RedirectToPage("/Stock/Index");
            }

            return await HandleFailureAsync(batch) ?? Page();
        }

        Batch = batch.Value;

        var history = await _api.GetBatchAdjustmentsAsync(
            BatchId, PageNumber, pageSize: 20, ct: ct);

        if (!history.IsSuccess)
        {
            return await HandleFailureAsync(history) ?? Page();
        }

        Adjustments = history.Value ?? ApiPage<StockAdjustmentModel>.Empty;

        return Page();
    }
}
