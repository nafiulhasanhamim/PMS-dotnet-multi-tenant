using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PMS.Web.Pages;

/// <summary>
/// The generic failure page.
///
/// It says nothing specific on purpose - the detail was already written to the server log by
/// PmsApiClient or by the exception handler. A stack trace or an upstream error message on
/// screen helps nobody standing at a counter and can leak internals.
/// </summary>
[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorModel : PageModel
{
    private readonly ILogger<ErrorModel> _logger;

    public ErrorModel(ILogger<ErrorModel> logger)
    {
        _logger = logger;
    }

    public string? RequestId { get; private set; }

    /// <summary>Set when re-executed by the status-code middleware, e.g. a 404.</summary>
    [BindProperty(SupportsGet = true, Name = "status")]
    public int? StatusCode { get; set; }

    public string Title => StatusCode switch
    {
        404 => "Page not found",
        403 => "Permission denied",
        _ => "Something went wrong",
    };

    public string Message => StatusCode switch
    {
        404 => "That page does not exist. It may have been moved, or the link may be wrong.",
        403 => "You do not have permission to view that page.",
        _ => "The request could not be completed. Please try again, and tell your "
             + "administrator if it keeps happening.",
    };

    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        // Logged with the same id shown on screen, so a reported "reference" can be found.
        if (StatusCode is null or >= 500)
        {
            _logger.LogWarning(
                "Error page shown for {Path} with reference {RequestId}.",
                Request.Path, RequestId);
        }
    }
}
