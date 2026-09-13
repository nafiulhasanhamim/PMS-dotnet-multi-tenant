using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PMS.Web.Auth;

namespace PMS.Web.Pages;

/// <summary>
/// The permission-denied page.
///
/// Anonymous on purpose. It is reached in two ways: the cookie handler redirects here when a
/// policy fails, and a page redirects here after the API answered 403 - and in the
/// tenant-unavailable case the session has just been signed out, so requiring authentication
/// would bounce the visitor to a login page instead of telling them what happened.
/// </summary>
[AllowAnonymous]
public class DeniedModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Reason { get; set; }

    /// <summary>Which login to offer, based on whichever session still exists.</summary>
    public bool IsPlatformContext { get; private set; }

    public void OnGet()
    {
        IsPlatformContext = User.IsPlatformAdmin()
            || Request.Path.StartsWithSegments("/platform", StringComparison.OrdinalIgnoreCase)
            || (Request.Headers.Referer.ToString()
                .Contains("/platform", StringComparison.OrdinalIgnoreCase));
    }
}
