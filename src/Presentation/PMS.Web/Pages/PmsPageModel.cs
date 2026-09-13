using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Pages;

/// <summary>
/// Shared behaviour for every page that calls the API on a signed-in user's behalf.
///
/// The reason this exists is <see cref="HandleFailureAsync"/>. Each page could decide for
/// itself what an API 401 or 403 means, and they would drift — one signing the user out, one
/// showing a blank table, one rendering a raw status code. Deciding once here means a later
/// module inherits the behaviour instead of reinventing it.
/// </summary>
public abstract class PmsPageModel : PageModel
{
    /// <summary>A form-level error to render as a banner at the top of the page.</summary>
    public string? FormError { get; private set; }

    /// <summary>A success notice, carried across a redirect.</summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>The scheme this page's session belongs to. Governs which cookie is cleared.</summary>
    protected virtual string Scheme => WebSchemes.Tenant;

    /// <summary>Where an ended session should send the visitor back to.</summary>
    protected virtual string LoginPage => "/Login";

    /// <summary>
    /// Decides what a failed API call means.
    ///
    /// Returns an <see cref="IActionResult"/> when the page should stop and go elsewhere, or
    /// null when the failure is the page's own to display — in which case the message is
    /// already on <see cref="FormError"/> and any field errors are on ModelState.
    /// </summary>
    protected async Task<IActionResult?> HandleFailureAsync<T>(ApiResult<T> result)
    {
        var problem = result.Problem;

        switch (problem?.Failure)
        {
            case ApiFailure.SessionExpired:
                // The cookie outlived the token. Clearing it is the only honest response —
                // otherwise the person keeps clicking a UI where nothing works.
                await HttpContext.SignOutAsync(Scheme);
                return RedirectToPage(LoginPage, new { expired = true });

            case ApiFailure.TenantUnavailable:
                // Suspended or deleted. The session names a pharmacy that can no longer be
                // used, so the cookie is misleading and has to go.
                await HttpContext.SignOutAsync(Scheme);
                return RedirectToPage("/Denied", new { reason = problem.Message });

            case ApiFailure.Forbidden:
                // Role wasn't enough. The session is still perfectly valid — do NOT sign out,
                // or a mis-click would log people out of a working session.
                return RedirectToPage("/Denied");

            case ApiFailure.ServerError:
                // Detail was already logged by the client; the visitor gets nothing specific.
                return RedirectToPage("/Error");

            default:
                ApplyProblem(problem);
                return null;
        }
    }

    /// <summary>
    /// Puts an API rejection where the form can render it: field messages under their inputs,
    /// anything else as a banner.
    ///
    /// This is what makes the API the authoritative validator rather than a second opinion —
    /// its per-field messages land on the same inputs client-side hints would have flagged.
    /// </summary>
    protected void ApplyProblem(ApiProblem? problem)
    {
        if (problem is null)
        {
            FormError = "Something went wrong.";
            return;
        }

        var boundAnyField = false;

        foreach (var (field, messages) in problem.FieldErrors)
        {
            var key = ModelStateKeyFor(field);

            foreach (var message in messages)
            {
                ModelState.AddModelError(key, message);
            }

            boundAnyField = true;
        }

        if (boundAnyField)
        {
            return;
        }

        // A 409 is not a validation failure, so it carries no `errors` dictionary — but for a
        // duplicate domain or a duplicate email the problem *is* one field, and a page-level
        // banner is the wrong place to say so. A page names the field a conflict belongs to
        // and the message lands under the input the person has to change.
        if (problem.Status == StatusCodes.Status409Conflict && ConflictField is not null)
        {
            ModelState.AddModelError(ModelStateKeyFor(ConflictField), problem.Message);
            return;
        }

        FormError = problem.Message;
    }

    /// <summary>
    /// Which input a bare conflict should be attached to. Null puts it in the banner.
    /// </summary>
    protected virtual string? ConflictField => null;

    protected void ShowError<T>(ApiResult<T> result) => ApplyProblem(result.Problem);

    protected void ShowError(string message) => FormError = message;

    /// <summary>
    /// Maps an API property name onto this page's ModelState key.
    ///
    /// The API says <c>DomainName</c>; the form binds <c>Input.DomainName</c>. Pages whose
    /// input is not under an <c>Input</c> property override this.
    /// </summary>
    protected virtual string ModelStateKeyFor(string apiField) => $"Input.{apiField}";
}

/// <summary>Base for pages inside the platform area, so they clear the right cookie.</summary>
public abstract class PlatformPageModel : PmsPageModel
{
    protected override string Scheme => WebSchemes.Platform;

    protected override string LoginPage => "/Platform/Login";
}
