using System.Reflection;
using PMS.Domain.Enums;
using PMS.WebApi.Access;
using PMS.WebApi.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace PMS.ArchitectureTests.Access;

/// <summary>
/// Guards the one part of the access matrix that can go wrong quietly.
///
/// <para>The matrix is derived from the routing table, so it cannot drift from the policies —
/// but it does depend on <see cref="RoleAccessMap"/> knowing how to translate each policy name
/// into a role set. A policy added without being taught to the map would show on the platform
/// page as reachable by nobody: safe, but wrong, and wrong in a way an operator reading the
/// page would have no way to detect.</para>
///
/// <para>So these tests fail the build instead.</para>
/// </summary>
public class RoleAccessMapTests
{
    /// <summary>Every policy name declared on <c>AuthenticationExtensions</c>.</summary>
    private static IEnumerable<string> DeclaredPolicies() =>
        typeof(AuthenticationExtensions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!);

    [Fact]
    public void EveryDeclaredPolicy_IsTranslatableToRoles()
    {
        var unknown = DeclaredPolicies()
            .Where(policy => !RoleAccessMap.IsKnown(policy))
            .ToList();

        unknown.Should().BeEmpty(
            "every authorization policy has to be mapped to the roles that satisfy it, or the "
            + "platform access page reports it as reachable by nobody. Add it to "
            + "RoleAccessMap.RolesFor.");
    }

    /// <summary>
    /// Every policy used on a controller or action is one the map knows.
    ///
    /// <para>Broader than the test above: this catches a policy name typed as a literal at a
    /// call site rather than referenced from the constants.</para>
    /// </summary>
    [Fact]
    public void EveryPolicyUsedOnAnEndpoint_IsTranslatableToRoles()
    {
        var used = typeof(Program).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type
                .GetCustomAttributes<AuthorizeAttribute>()
                .Concat(type
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>())))
            .Select(attribute => attribute.Policy)
            .Where(policy => !string.IsNullOrEmpty(policy))
            .Distinct()
            .ToList();

        used.Should().NotBeEmpty("the API is expected to use policies on its endpoints");

        used.Where(policy => !RoleAccessMap.IsKnown(policy))
            .Should().BeEmpty("a policy in use on an endpoint must be translatable to roles");
    }

    [Fact]
    public void EveryRole_HasAColumnAndADescription()
    {
        var roles = Enum.GetValues<UserRole>();

        RoleAccessMap.Columns.Should().BeEquivalentTo(roles,
            "a role missing from the matrix would silently not be reported on at all");

        foreach (var role in roles)
        {
            RoleAccessMap.Name(role).Should().NotBeNullOrWhiteSpace();
            RoleAccessMap.Describe(role).Should().NotBeNullOrWhiteSpace();
        }
    }

    /// <summary>
    /// The claim the whole separation rests on: a platform token carries no tenant, so it
    /// satisfies no tenant policy.
    ///
    /// <para>Asserted here rather than left to the reader of the matrix, because "the platform
    /// admin can do everything" is the natural assumption and it is false. If somebody ever
    /// adds PlatformAdmin to a tenant policy's role set to make the page look tidier, this
    /// fails.</para>
    /// </summary>
    [Theory]
    [InlineData(AuthenticationExtensions.TenantUserPolicy)]
    [InlineData(AuthenticationExtensions.TenantWriterPolicy)]
    [InlineData(AuthenticationExtensions.TenantAdminPolicy)]
    public void APlatformAdmin_SatisfiesNoTenantPolicy(string policy)
        => RoleAccessMap.RolesFor(policy)!.Should().NotContain(UserRole.PlatformAdmin);

    [Fact]
    public void ATenantRole_DoesNotSatisfyThePlatformPolicy()
    {
        var allowed = RoleAccessMap.RolesFor(AuthenticationExtensions.PlatformAdminPolicy)!;

        allowed.Should().Equal(UserRole.PlatformAdmin);
    }

    /// <summary>
    /// The role sets are nested the way the policy names imply: an Admin can reach everything
    /// a writer can, and a writer everything a viewer can.
    /// </summary>
    [Fact]
    public void TenantPolicies_AreNestedFromAdminOutwards()
    {
        var admin = RoleAccessMap.RolesFor(AuthenticationExtensions.TenantAdminPolicy)!;
        var writer = RoleAccessMap.RolesFor(AuthenticationExtensions.TenantWriterPolicy)!;
        var user = RoleAccessMap.RolesFor(AuthenticationExtensions.TenantUserPolicy)!;

        admin.Should().BeSubsetOf(writer);
        writer.Should().BeSubsetOf(user);

        writer.Should().Contain(UserRole.Pharmacist,
            "a pharmacist is the person who knows what the pharmacy stocks");
        writer.Should().NotContain(UserRole.Employee,
            "an Employee may look and may not change anything");
        admin.Should().NotContain(UserRole.Pharmacist,
            "the two policies differ, or TenantWriter would not need to exist");
    }
}
