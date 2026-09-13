using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;

namespace PMS.Web.Pages;

public class AccountModel : PmsPageModel
{
    private readonly PmsApiClient _api;

    public AccountModel(PmsApiClient api)
    {
        _api = api;
    }

    public MyProfile? Profile { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetMyProfileAsync(ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Profile = result.Value;

        return Page();
    }
}
