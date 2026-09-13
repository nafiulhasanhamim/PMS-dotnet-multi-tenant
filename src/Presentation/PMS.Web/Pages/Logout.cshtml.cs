using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PMS.Web.Auth;

namespace PMS.Web.Pages;

public class LogoutModel : PageModel
{
    /// <summary>
    /// POST only. A GET logout link can be fired by any page on the internet with an
    /// img tag, which would let a hostile site sign people out at will.
    ///
    /// Signing out of the cookie discards the stored JWT with it - the token lives inside
    /// the ticket, so there is nothing left server-side to clean up separately.
    /// </summary>
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(WebSchemes.Tenant);

        return RedirectToPage("/Login");
    }

    public IActionResult OnGet() => RedirectToPage("/Index");
}
