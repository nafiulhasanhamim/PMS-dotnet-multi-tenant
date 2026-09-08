using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;

namespace PMS.Web.Pages;

public class IndexModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public IndexModel(PmsApiClient api)
    {
        _api = api;
    }

    public MyProfile? Profile { get; private set; }

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

        return Page();
    }
}
