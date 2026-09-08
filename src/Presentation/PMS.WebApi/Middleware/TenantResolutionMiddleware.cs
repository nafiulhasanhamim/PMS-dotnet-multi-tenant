using PMS.Application.Interfaces;
using PMS.SharedKernel.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace PMS.WebApi.Middleware;

/// <summary>
/// Stops a suspended or deleted pharmacy being used, once per request.
///
/// Login is the first gate — no token is issued for an inactive tenant. This is the second:
/// without it, suspending a pharmacy would not take effect until every token already issued
/// had expired, so a pharmacy suspended for non-payment would keep trading for hours.
///
/// The check deliberately lives here rather than in the query filter. Suspension withdraws
/// *access*; it does not mean the pharmacy's data stopped existing. Joining every query to
/// Tenants to prove the owner is still active would cost that join on every read, forever,
/// to enforce something that only changes when an administrator suspends someone.
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentTenantService tenantContext,
        ITenantStatusValidator validator)
    {
        // Only validate what the request actually claims. A request with no tenant — login,
        // health, swagger, or a platform administrator — has nothing to check, and the query
        // filter already shows it no tenant data.
        if (!tenantContext.HasTenant)
        {
            await _next(context);
            return;
        }

        var status = await validator.CheckAsync(tenantContext.TenantId, context.RequestAborted);
        if (status == TenantStatus.Ok)
        {
            await _next(context);
            return;
        }

        await WriteRefusal(context, status);
    }

    private static async Task WriteRefusal(HttpContext context, TenantStatus status)
    {
        var problem = status == TenantStatus.Inactive
            ? new ProblemDetails
            {
                Title = "Pharmacy suspended",
                Detail = "This pharmacy's access has been suspended. Contact your administrator.",
                Status = StatusCodes.Status403Forbidden,
            }
            : new ProblemDetails
            {
                Title = "Pharmacy not found",
                // Deliberately vague: whether a given tenant id exists is not something an
                // unauthenticated caller should be able to probe for.
                Detail = "This pharmacy is no longer available. Please sign in again.",
                Status = StatusCodes.Status403Forbidden,
            };

        problem.Extensions["code"] = status == TenantStatus.Inactive
            ? "Tenant.Inactive"
            : "Tenant.NotFound";

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}

public static class TenantStatusMiddlewareExtensions
{
    /// <summary>
    /// Must be registered after UseAuthentication — the tenant comes from a claim, and there
    /// are no claims until authentication has run.
    /// </summary>
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}
