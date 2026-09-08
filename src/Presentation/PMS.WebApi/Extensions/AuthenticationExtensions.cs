using System.Text;
using PMS.Application.Common.Security;
using PMS.Domain.Enums;
using PMS.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace PMS.WebApi.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>Policy for platform operators — requires the platform_admin claim.</summary>
    public const string PlatformAdminPolicy = "PlatformAdmin";

    /// <summary>Policy for a pharmacy's own administrator.</summary>
    public const string TenantAdminPolicy = "TenantAdmin";

    /// <summary>Policy for anyone signed in *at a pharmacy* (any tenant role).</summary>
    public const string TenantUserPolicy = "TenantUser";

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        var options = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            // Refuse to start rather than fall back to a built-in key: a predictable signing
            // key means anyone can mint a token for any pharmacy.
            throw new InvalidOperationException(
                "Jwt:SigningKey is not configured. Set it in configuration or user-secrets; " +
                "there is deliberately no default.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(bearer =>
            {
                // Keep the claim names the token was issued with.
                //
                // On by default, inbound mapping rewrites well-known short names to the old
                // WS-* URIs: "role" becomes .../claims/role and "sub" becomes
                // .../claims/nameidentifier. Everything in this app looks for the short names
                // (AuthClaims), so with mapping on, RequireClaim("role", "Admin") matched
                // nothing and every pharmacy-Admin endpoint answered 403 for a valid Admin
                // token — while the tenant-only endpoints kept working, which made it look
                // like an authorization bug rather than a renaming one.
                bearer.MapInboundClaims = false;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = options.Issuer,
                    ValidAudience = options.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(options.SigningKey)),
                    // No leeway: a suspended pharmacy's token should stop working when it
                    // says it does.
                    ClockSkew = TimeSpan.Zero,
                    // Match what the token carries, so User.IsInRole and RequireRole agree
                    // with the RequireClaim checks below.
                    RoleClaimType = AuthClaims.Role,
                    NameClaimType = AuthClaims.Subject,
                };
            });

        services.AddAuthorizationBuilder()
            // A platform token carries platform_admin and no tenant.
            .AddPolicy(PlatformAdminPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(AuthClaims.PlatformAdmin, "true"))
            // A pharmacy Admin: must have a tenant *and* the Admin role. Requiring the tenant
            // claim matters — it stops a platform token satisfying a tenant policy.
            .AddPolicy(TenantAdminPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(AuthClaims.TenantId)
                .RequireClaim(AuthClaims.Role, nameof(UserRole.Admin)))
            .AddPolicy(TenantUserPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(AuthClaims.TenantId));

        return services;
    }
}
