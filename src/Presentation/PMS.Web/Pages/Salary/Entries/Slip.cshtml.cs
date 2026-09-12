using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Salary.Entries;

/// <summary>
/// The printable salary slip, following the invoice's print approach from Module 5.
///
/// <para>Available on an unpaid entry too. Handing somebody the breakdown before the money moves
/// is how a disagreement gets settled before it becomes one - and an unpaid slip says so on its
/// face, so it cannot be mistaken for a receipt.</para>
/// </summary>
[Authorize(Policy = WebPolicies.TenantAdmin)]
public class SlipModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public SlipModel(PmsApiClient api) => _api = api;

    [BindProperty(SupportsGet = true, Name = "id")]
    public Guid EntryId { get; set; }

    public SalarySlip Slip { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetSalarySlipAsync(EntryId, ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? NotFound();
        }

        Slip = result.Value!;

        return Page();
    }
}
