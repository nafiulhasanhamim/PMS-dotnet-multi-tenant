using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using PMS.Web.Api;
using PMS.Web.Auth;
using PMS.Web.Tenancy;
using PMS.Web.Time;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────────────────────────────────
// This app is a *client* of PMS.WebApi. It references no other project in the solution and
// reaches the database only through HTTP calls to that API — the same way a mobile or
// desktop client will. See docs/frontend/01-auth-and-layout.md.
// ─────────────────────────────────────────────────────────────────────────────────────────

builder.Services.AddRazorPages(options =>
{
    // Each area is authorized by folder, and each policy names its own cookie scheme. That
    // pairing is what stops a platform cookie from satisfying a tenant page: the tenant
    // policy does not merely reject a platform principal, it never authenticates one.
    options.Conventions.AuthorizeFolder("/Platform", WebPolicies.PlatformAdmin);
    options.Conventions.AuthorizeFolder("/Users", WebPolicies.TenantAdmin);

    // Module 2. The lists are readable by every pharmacy user, including an Employee -
    // looking up what the pharmacy stocks is the job. Everything that writes is Admin or
    // Pharmacist, and each write page also carries its own [Authorize] attribute.
    options.Conventions.AuthorizeFolder("/Products", WebPolicies.TenantUser);
    options.Conventions.AuthorizeFolder("/Medicines", WebPolicies.TenantUser);
    options.Conventions.AuthorizeFolder("/OtherItems", WebPolicies.TenantUser);
    options.Conventions.AuthorizePage("/Index", WebPolicies.TenantUser);
    options.Conventions.AuthorizePage("/Account", WebPolicies.TenantUser);
    options.Conventions.AuthorizePage("/Logout", WebPolicies.TenantUser);

    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/Platform/Login");
    options.Conventions.AllowAnonymousToPage("/Denied");
    options.Conventions.AllowAnonymousToPage("/Error");
});

// Lowercase generated URLs, so the routes are the ones the spec names: /platform/tenants,
// /users/create. Razor Pages matches case-insensitively either way; this is about the links
// the app hands out being the canonical ones.
builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITokenAccessor, CookieTokenAccessor>();

// Which pharmacy a request is for, read from the address rather than typed by the person.
var tenancy = new TenancyOptions();
builder.Configuration.GetSection(TenancyOptions.SectionName).Bind(tenancy);
builder.Services.AddSingleton(tenancy);
builder.Services.AddSingleton<ITenantHostResolver, TenantHostResolver>();
builder.Services.AddTransient<ApiTokenHandler>();

// The API deals only in UTC. This is the one component that converts to the zone the person
// reading the screen lives in — see DisplayTimeZone for why it is the only one.
builder.Services.Configure<DisplayOptions>(
    builder.Configuration.GetSection(DisplayOptions.SectionName));
builder.Services.AddSingleton<DisplayTimeZone>();

var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException(
        "Api:BaseUrl is not configured — this app cannot work without knowing where the API is.");

builder.Services
    .AddHttpClient<PmsApiClient>(client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    // Every request on this client gets the stored JWT, so no call site can forget one.
    .AddHttpMessageHandler<ApiTokenHandler>();

// ─────────────────────────────────────────────────────────────────────────────────────────
// Two cookie schemes. See WebSchemes for why they are separate rather than one scheme with
// two policies.
// ─────────────────────────────────────────────────────────────────────────────────────────

// Secure cookies mean HTTPS, and the ASP.NET development certificate covers "localhost"
// only — not "popular-pharmacy.localhost". Rather than require a hand-made wildcard
// certificate to run the app locally, Development allows the cookie over plain HTTP. Every
// other environment keeps Always, which is what actually matters.
var secureCookiePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;

void ConfigureCookie(CookieAuthenticationOptions cookie, string name, string loginPath)
{
    cookie.Cookie.Name = name;
    cookie.Cookie.HttpOnly = true;                    // out of JavaScript's reach
    cookie.Cookie.SameSite = SameSiteMode.Strict;     // never sent cross-site
    cookie.Cookie.SecurePolicy = secureCookiePolicy;  // HTTPS only outside Development

    cookie.LoginPath = loginPath;
    cookie.AccessDeniedPath = "/denied";
    cookie.ReturnUrlParameter = "returnUrl";

    // Not sliding: the cookie carries a JWT with a fixed expiry, so extending the cookie
    // would leave someone "signed in" holding a dead token.
    cookie.SlidingExpiration = false;
}

builder.Services
    .AddAuthentication(WebSchemes.Tenant)
    .AddCookie(WebSchemes.Tenant, cookie =>
        ConfigureCookie(cookie, WebSchemes.TenantCookie, "/login"))
    .AddCookie(WebSchemes.Platform, cookie =>
        ConfigureCookie(cookie, WebSchemes.PlatformCookie, "/platform/login"));

builder.Services.AddAuthorizationBuilder()
    // Secure by default: a page with no authorization metadata of its own still requires a
    // signed-in user, in either scheme. A fallback policy rather than AuthorizeFolder("/"),
    // because a blanket folder rule would stack a second, tenant-scheme requirement onto the
    // platform pages and lock the platform out of its own area.
    .SetFallbackPolicy(new AuthorizationPolicyBuilder(WebSchemes.Tenant, WebSchemes.Platform)
        .RequireAuthenticatedUser()
        .Build())
    // Each policy names the scheme it accepts. Without that, a policy would be evaluated
    // against the default scheme's principal and the separation would be cosmetic.
    .AddPolicy(WebPolicies.TenantUser, policy => policy
        .AddAuthenticationSchemes(WebSchemes.Tenant)
        .RequireAuthenticatedUser()
        .RequireClaim(WebClaims.TenantId))
    .AddPolicy(WebPolicies.TenantAdmin, policy => policy
        .AddAuthenticationSchemes(WebSchemes.Tenant)
        .RequireAuthenticatedUser()
        .RequireClaim(WebClaims.TenantId)
        .RequireRole(nameof(UserRole.Admin)))
    // Admin or Pharmacist. Listing both role values on one RequireRole is an OR.
    .AddPolicy(WebPolicies.TenantWriter, policy => policy
        .AddAuthenticationSchemes(WebSchemes.Tenant)
        .RequireAuthenticatedUser()
        .RequireClaim(WebClaims.TenantId)
        .RequireRole(nameof(UserRole.Admin), nameof(UserRole.Pharmacist)))
    .AddPolicy(WebPolicies.PlatformAdmin, policy => policy
        .AddAuthenticationSchemes(WebSchemes.Platform)
        .RequireAuthenticatedUser()
        .RequireClaim(WebClaims.IsPlatformAdmin, "true"));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}
else
{
    // In development an unhandled page exception should be visible, not swallowed into a
    // friendly page that hides the stack trace.
    app.UseDeveloperExceptionPage();
}

app.UseStatusCodePagesWithReExecute("/error", "?status={0}");

// Not in Development, and for the same reason the cookie relaxes to SameAsRequest there: a
// pharmacy is reached at its own subdomain, and the ASP.NET development certificate covers
// "localhost" only. Forcing https locally would send every subdomain to a certificate warning.
// Outside Development it is on, and HSTS with it.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
