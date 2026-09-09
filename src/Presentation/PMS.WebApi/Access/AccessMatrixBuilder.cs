using System.Text;
using PMS.Domain.Enums;
using PMS.SharedKernel.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

namespace PMS.WebApi.Access;

/// <summary>
/// Reads the access matrix out of the running application.
///
/// <para><b>Derived, not written down.</b> It walks <see cref="EndpointDataSource"/> — the same
/// routing table the server dispatches on — and reads each endpoint's authorization metadata.
/// A new controller action appears here the moment it exists, and an action whose policy
/// changes shows its new roles on the next page load. A hand-maintained table would be correct
/// on the day it was written and quietly wrong within a release, which for an access review is
/// worse than having no table: it invites a decision based on a stale one.</para>
///
/// <para>The one thing it cannot see is what a handler withholds <em>inside</em> a response.
/// Those are declared by hand in <see cref="WithheldFieldCatalog"/> — deliberately a
/// separate file, so the part somebody has to remember to update is not buried inside the
/// part that updates itself — and the page labels them as declared.</para>
/// </summary>
public sealed class AccessMatrixBuilder
{
    private readonly EndpointDataSource _endpoints;
    private readonly IDateTime _clock;

    public AccessMatrixBuilder(EndpointDataSource endpoints, IDateTime clock)
    {
        _endpoints = endpoints;
        _clock = clock;
    }

    public AccessMatrixDto Build()
    {
        var entries = new List<(string Area, AccessEntryDto Entry)>();

        foreach (var endpoint in _endpoints.Endpoints.OfType<RouteEndpoint>())
        {
            // Controller actions only. The infrastructure endpoints — Swagger UI, the static
            // files it needs — are not part of the access model and listing them would bury
            // the rows that are.
            var descriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();

            if (descriptor is null)
            {
                continue;
            }

            var anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;

            // Ordered: controller-level attributes then action-level. Both are in force.
            var policies = endpoint.Metadata
                .GetOrderedMetadata<IAuthorizeData>()
                .Select(data => data.Policy)
                .Where(policy => policy is not null)
                .Select(policy => policy!)
                .Distinct()
                .ToList();

            var allowed = anonymous || policies.Count == 0
                ? RoleAccessMap.Columns.ToList()
                : Intersect(policies);

            var method = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()
                ?.HttpMethods.FirstOrDefault() ?? "ANY";

            entries.Add((
                AreaName(descriptor.ControllerName),
                new AccessEntryDto(
                    descriptor.ActionName,
                    Humanise(descriptor.ActionName),
                    method,
                    "/" + (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/'),
                    policies,
                    RequiresAuthentication: !anonymous && policies.Count > 0,
                    allowed)));
        }

        var areas = entries
            .GroupBy(e => e.Area)
            .OrderBy(group => AreaOrder(group.Key))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AccessAreaDto(
                group.Key,
                group
                    .Select(e => e.Entry)
                    .OrderBy(entry => entry.Route, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.Method, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .ToList();

        var authenticated = entries
            .Where(e => e.Entry.RequiresAuthentication)
            .ToList();

        var roles = RoleAccessMap.Columns
            .Select(role => new AccessRoleDto(
                role,
                RoleAccessMap.Name(role),
                RoleAccessMap.Describe(role),
                authenticated.Count(e => e.Entry.AllowedRoles.Contains(role))))
            .ToList();

        return new AccessMatrixDto(
            roles,
            areas,
            WithheldFieldCatalog.Entries,
            entries.Count,
            entries.Count - authenticated.Count,
            _clock.UtcNow);
    }

    /// <summary>
    /// The roles that satisfy <b>every</b> policy in force — an intersection.
    ///
    /// <para>An unknown policy contributes an empty set, so an endpoint guarded by something
    /// this map has not been taught shows as reachable by nobody. Failing closed is the right
    /// direction for a screen somebody makes access decisions from; the test that guards the
    /// map is what stops it happening quietly.</para>
    /// </summary>
    private static List<UserRole> Intersect(IEnumerable<string> policies)
    {
        IEnumerable<UserRole> allowed = RoleAccessMap.Columns;

        foreach (var policy in policies)
        {
            var forPolicy = RoleAccessMap.RolesFor(policy) ?? new HashSet<UserRole>();
            allowed = allowed.Where(forPolicy.Contains).ToList();
        }

        return RoleAccessMap.Columns.Where(allowed.ToHashSet().Contains).ToList();
    }

    /// <summary>Controller name to something a person reads.</summary>
    private static string AreaName(string controller) => controller switch
    {
        "Auth" => "Signing in",
        "PlatformAuth" => "Signing in",
        "PlatformTenants" => "Platform — pharmacies",
        "PlatformAccess" => "Platform — access",
        "Users" => "Pharmacy staff",
        "Products" => "Product catalogue",
        "CatalogMedicines" => "Reference catalogue",
        "Stock" => "Stock and batches",
        _ => Humanise(controller),
    };

    private static int AreaOrder(string area) => area switch
    {
        "Signing in" => 0,
        "Platform — pharmacies" => 1,
        "Platform — access" => 2,
        "Pharmacy staff" => 3,
        "Product catalogue" => 4,
        "Reference catalogue" => 5,
        "Stock and batches" => 6,
        _ => 9,
    };

    /// <summary>
    /// "GetProductStock" becomes "Get product stock".
    ///
    /// <para>Derived from the method name rather than a description somebody writes, for the
    /// same reason as everything else here: a label that has to be maintained is a label that
    /// goes stale. The route is shown alongside, and it is unambiguous where the label is only
    /// approximate.</para>
    /// </summary>
    private static string Humanise(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var text = new StringBuilder();

        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
            {
                text.Append(' ');
                text.Append(char.ToLowerInvariant(name[i]));
                continue;
            }

            text.Append(i == 0 ? char.ToUpperInvariant(name[i]) : name[i]);
        }

        return text.ToString();
    }
}
