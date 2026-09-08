using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.Tenancy;

namespace PMS.Web.Pages;

/// <summary>
/// Pharmacy sign in.
///
/// The pharmacy is never asked for. It comes from the address, and the form asks only for
/// email and password — the two things a person actually knows. An identifier they have to be
/// told, and then retype every morning, is friction on every single sign-in.
///
/// The page therefore has two modes:
///
/// <list type="bullet">
/// <item><b>Credentials</b> — the address names a pharmacy. Email, password, done.</item>
/// <item><b>Signpost</b> — the address names none (the base domain, or a reserved prefix).
/// There is no credential form at all here, because there is nothing to sign in to yet. It
/// asks for the pharmacy's short name and <em>navigates</em> to that pharmacy's own address.
/// That is a redirect, not a login: no credentials are collected on this mode.</item>
/// </list>
/// </summary>
[AllowAnonymous]
public class LoginModel : PmsPageModel
{
    private readonly PmsApiClient _api;
    private readonly ITenantHostResolver _hostResolver;

    public LoginModel(PmsApiClient api, ITenantHostResolver hostResolver)
    {
        _api = api;
        _hostResolver = hostResolver;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Set when the API rejected a token and the session had to be ended.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Expired { get; set; }

    /// <summary>
    /// Where the visitor was heading when they were bounced here.
    ///
    /// Its presence is itself a signal worth acting on: it means the cookie handler turned an
    /// authenticated request away, which in this app almost always means the session ran out
    /// — the cookie's lifetime is pinned to the token's, so it usually lapses before any API
    /// call can return a 401. Without this, the far more common expiry path would drop people
    /// on a bare login form with no idea why.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "returnUrl")]
    public string? ReturnUrl { get; set; }

    /// <summary>Whether to explain that the previous session ended.</summary>
    public bool ShowSessionEnded => Expired || !string.IsNullOrEmpty(ReturnUrl);

    /// <summary>
    /// Forces signpost mode on an address that does name a pharmacy.
    ///
    /// Reached from "Sign in to a different pharmacy". Someone who works at two pharmacies has
    /// to be able to leave the one the address picked for them; without this the address would
    /// be a trap rather than a shortcut.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "find")]
    public bool FindPharmacy { get; set; }

    /// <summary>Prefills the signpost from <c>/login?domain=popular-pharmacy</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "domain")]
    public string? Domain { get; set; }

    /// <summary>The host that identifies the pharmacy, if the address names one.</summary>
    public string? HostPharmacy { get; private set; }

    /// <summary>That host as it should be shown — the prefix, or the whole address.</summary>
    public string? HostPharmacyDisplay =>
        HostPharmacy is null ? null : _hostResolver.Display(HostPharmacy);

    /// <summary>True when the address decided the pharmacy, so credentials can be asked for.</summary>
    public bool HasPharmacy => HostPharmacy is not null && !FindPharmacy;

    /// <summary>The base domain, named on the signpost so the address is guessable.</summary>
    public string? BaseDomain => _hostResolver.BaseDomain;

    public sealed class InputModel
    {
        /// <summary>Filled from the address, never from a field on screen.</summary>
        public string DomainName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        /// <summary>Signpost only: the pharmacy's short name, to navigate to.</summary>
        public string PharmacyName { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true && User.TenantName() is not null)
        {
            return RedirectToPage("/Index");
        }

        HostPharmacy = _hostResolver.Resolve(Request);

        if (HasPharmacy)
        {
            Input.DomainName = HostPharmacy!;
        }
        else if (!string.IsNullOrWhiteSpace(Domain))
        {
            Input.PharmacyName = Domain.Trim();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        HostPharmacy = _hostResolver.Resolve(Request);

        if (!HasPharmacy)
        {
            // No pharmacy in the address means no credential form was rendered, so a post
            // here is not a sign-in attempt. Re-render the signpost rather than trying.
            return Page();
        }

        // Taken from the address, never from the request body. There is no field on screen for
        // a person to have changed, so a domain arriving in the post came from somewhere else.
        Input.DomainName = HostPharmacy!;

        // Convenience checks only. The API validates too, and its answer is the one that
        // decides — see ApplyProblem, which puts the API's field messages on these inputs.
        if (string.IsNullOrWhiteSpace(Input.Email))
        {
            ModelState.AddModelError("Input.Email", "Enter your email.");
        }

        if (string.IsNullOrWhiteSpace(Input.Password))
        {
            ModelState.AddModelError("Input.Password", "Enter your password.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _api.TenantLoginAsync(
            new TenantLoginRequest(Input.DomainName, Input.Email.Trim(), Input.Password), ct);

        if (!result.IsSuccess || result.Value is null)
        {
            // One banner, one message, whatever went wrong. The API deliberately does not say
            // whether the pharmacy, the email or the password was the problem — it will not
            // confirm which pharmacies exist or who works there — and neither will this page.
            ShowError(result.ErrorMessage);
            return Page();
        }

        await HttpContext.SignInTenantUserAsync(result.Value);

        return RedirectToPage("/Index");
    }

    /// <summary>
    /// Signpost mode: send the visitor to their pharmacy's own address.
    ///
    /// Only a short name is accepted, and the destination is always built as
    /// <c>{name}.{baseDomain}</c>. That is deliberate and load-bearing: redirecting to a host
    /// somebody typed would be an open redirect, and this page is reachable without signing
    /// in. A pharmacy that uses its own domain is not reachable this way at all — it has its
    /// own address, and the page says so.
    /// </summary>
    public IActionResult OnPostFind()
    {
        HostPharmacy = _hostResolver.Resolve(Request);

        var name = Input.PharmacyName?.Trim().Trim('.').ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError("Input.PharmacyName", "Enter your pharmacy's name.");
            return Page();
        }

        if (string.IsNullOrEmpty(BaseDomain))
        {
            ModelState.AddModelError(
                "Input.PharmacyName",
                "No platform address is configured. Use the link your pharmacy gave you.");
            return Page();
        }

        // Anything with a dot is a whole address, and following one typed here would let this
        // page redirect anywhere on the internet.
        if (name.Contains('.') || name.Contains('/') || name.Contains(':'))
        {
            ModelState.AddModelError(
                "Input.PharmacyName",
                "Enter just the short name, without dots or slashes. If your pharmacy uses its "
                + "own web address, open that address directly.");
            return Page();
        }

        // Not validated against the tenant list on purpose. Confirming here which pharmacies
        // exist would hand an anonymous visitor exactly what the login endpoint refuses to
        // reveal; an unknown name simply lands on a page whose sign-in then fails generically.
        var target = _hostResolver.SignInUrl(name, Request.IsHttps, Request.Host.Port);

        return Redirect(target);
    }
}
