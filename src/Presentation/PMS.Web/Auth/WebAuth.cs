using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using PMS.Web.Api;

namespace PMS.Web.Auth;

/// <summary>
/// The two cookie schemes this app issues.
///
/// A platform operator and a pharmacy user do not hold the same kind of session — one is
/// inside a pharmacy and one is deliberately outside every pharmacy — so they get separate
/// schemes with separate cookie names. That is not tidiness. With a single scheme, a policy
/// is the only thing standing between a platform session and a tenant page; with two, the
/// tenant scheme does not authenticate a platform cookie at all, so a platform session cannot
/// satisfy a tenant page's authorization even if a policy were written wrongly.
///
/// The practical consequence: signing in to one area does not sign you out of the other. Both
/// cookies can exist at once, and each area sees only its own.
/// </summary>
public static class WebSchemes
{
    public const string Tenant = "PmsTenant";
    public const string Platform = "PmsPlatform";

    public const string TenantCookie = "pms.tenant";
    public const string PlatformCookie = "pms.platform";
}

/// <summary>
/// Claim names inside <em>this app's</em> cookies — not the API's claim names. The cookie is
/// this app's own session; the API token is one item stored inside it.
/// </summary>
public static class WebClaims
{
    /// <summary>The API bearer token. Never leaves the server.</summary>
    public const string ApiToken = "pms:api_token";

    public const string UserId = "pms:user_id";
    public const string TenantId = "pms:tenant_id";
    public const string TenantName = "pms:tenant_name";
    public const string TenantDomain = "pms:tenant_domain";
    public const string IsPlatformAdmin = "pms:platform_admin";
}

public static class WebPolicies
{
    /// <summary>Any signed-in pharmacy user.</summary>
    public const string TenantUser = "TenantUserPolicy";

    /// <summary>A pharmacy user holding the Admin role at the pharmacy they signed in to.</summary>
    public const string TenantAdmin = "TenantAdminPolicy";

    /// <summary>
    /// A pharmacy user who may change things: Admin or Pharmacist, not Employee.
    ///
    /// Mirrors the API's TenantWriterPolicy. The two differ on exactly one action - only an
    /// Admin may deactivate - so deactivate buttons check the Admin policy instead.
    /// </summary>
    public const string TenantWriter = "TenantWriterPolicy";

    /// <summary>A platform operator.</summary>
    public const string PlatformAdmin = "PlatformAdminPolicy";
}

/// <summary>Reads the API bearer token for the current request.</summary>
public interface ITokenAccessor
{
    string? Token { get; }
}

/// <summary>
/// Takes the token out of whichever of this app's cookies authenticated the request.
///
/// The JWT is stored as a claim inside the ASP.NET authentication cookie, which data
/// protection encrypts and signs. Combined with HttpOnly that puts it beyond JavaScript's
/// reach entirely — no localStorage, no sessionStorage, nothing rendered into a page — so an
/// XSS bug cannot walk off with a pharmacy's session.
/// </summary>
public sealed class CookieTokenAccessor : ITokenAccessor
{
    private readonly IHttpContextAccessor _accessor;

    public CookieTokenAccessor(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public string? Token =>
        _accessor.HttpContext?.User?.FindFirst(WebClaims.ApiToken)?.Value;
}

/// <summary>
/// Attaches the stored JWT to every outgoing API call.
///
/// A handler rather than per-call code so that no future call can forget. The two login
/// endpoints are anonymous and simply have no token stored at that point, so they need no
/// special case — which also means a stray token can never be sent to them.
/// </summary>
public sealed class ApiTokenHandler : DelegatingHandler
{
    private readonly ITokenAccessor _tokens;

    public ApiTokenHandler(ITokenAccessor tokens)
    {
        _tokens = tokens;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _tokens.Token;

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

public static class SignInExtensions
{
    /// <summary>
    /// Turns a pharmacy login into a tenant cookie session.
    ///
    /// The cookie's lifetime is pinned to the token's own expiry so the two cannot drift: a
    /// cookie outliving its token leaves someone apparently signed in while every call fails.
    /// </summary>
    public static Task SignInTenantUserAsync(this HttpContext context, AuthResult auth)
    {
        if (auth.Tenant is null)
        {
            throw new InvalidOperationException(
                "A tenant sign-in needs a pharmacy. This result came from the platform endpoint.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, auth.UserId.ToString()),
            new(ClaimTypes.Name, auth.FullName),
            new(ClaimTypes.Email, auth.Email),
            new(ClaimTypes.Role, auth.Role.ToString()),
            new(WebClaims.UserId, auth.UserId.ToString()),
            new(WebClaims.TenantId, auth.Tenant.Id.ToString()),
            new(WebClaims.TenantName, auth.Tenant.Name),
            new(WebClaims.TenantDomain, auth.Tenant.DomainName),
            new(WebClaims.ApiToken, auth.Token),
        };

        return context.SignInAsync(
            WebSchemes.Tenant,
            new ClaimsPrincipal(new ClaimsIdentity(claims, WebSchemes.Tenant)),
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = auth.ExpiresAtUtc });
    }

    /// <summary>
    /// Turns a platform login into a platform cookie session. No tenant claims are written —
    /// there is no pharmacy to name, and writing one would be a lie a page could act on.
    /// </summary>
    public static Task SignInPlatformAdminAsync(this HttpContext context, AuthResult auth)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, auth.UserId.ToString()),
            new(ClaimTypes.Name, auth.FullName),
            new(ClaimTypes.Email, auth.Email),
            new(WebClaims.UserId, auth.UserId.ToString()),
            new(WebClaims.IsPlatformAdmin, "true"),
            new(WebClaims.ApiToken, auth.Token),
        };

        return context.SignInAsync(
            WebSchemes.Platform,
            new ClaimsPrincipal(new ClaimsIdentity(claims, WebSchemes.Platform)),
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = auth.ExpiresAtUtc });
    }
}

/// <summary>Convenience reads over whichever session authenticated the request.</summary>
public static class PrincipalExtensions
{
    public static bool IsPlatformAdmin(this ClaimsPrincipal user) =>
        user.HasClaim(WebClaims.IsPlatformAdmin, "true");

    public static string? TenantName(this ClaimsPrincipal user) =>
        user.FindFirst(WebClaims.TenantName)?.Value;

    public static string? TenantDomain(this ClaimsPrincipal user) =>
        user.FindFirst(WebClaims.TenantDomain)?.Value;

    public static string? FullName(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Name)?.Value;

    public static string? Email(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Email)?.Value;

    public static UserRole? Role(this ClaimsPrincipal user) =>
        Enum.TryParse<UserRole>(user.FindFirst(ClaimTypes.Role)?.Value, out var role)
            ? role
            : null;

    public static bool IsTenantAdmin(this ClaimsPrincipal user) =>
        user.TenantName() is not null && user.Role() == UserRole.Admin;
}
