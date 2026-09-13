using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PMS.Web.Auth;

namespace PMS.Web.Pages.Platform;

public class LogoutModel : PageModel
{
    /// <summary>
    /// Clears the platform cookie only. A tenant session in the same browser is a separate
    /// cookie and is deliberately left alone - the two are independent sessions.
    /// </summary>
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(WebSchemes.Platform);

        return RedirectToPage("/Platform/Login");
    }

    public IActionResult OnGet() => RedirectToPage("/Platform/Tenants/Index");
}
