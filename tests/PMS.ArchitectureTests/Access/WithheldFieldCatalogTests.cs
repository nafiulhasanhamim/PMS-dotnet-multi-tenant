using System.Reflection;
using PMS.WebApi.Access;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace PMS.ArchitectureTests.Access;

/// <summary>
/// Keeps the one hand-maintained part of the access matrix honest.
///
/// <para>The endpoint matrix is derived from the routing table, so it cannot go stale. The
/// withheld-field catalogue can: a field hidden from a role by a projection is invisible to
/// routing, so somebody has to declare it. That "somebody has to remember" is precisely the
/// weakness, and these tests remove it.</para>
///
/// <para><b>The convention they rely on.</b> A controller gates a field with a private
/// <c>bool CallerMaySee…</c> property, read from the caller's role and passed into the query,
/// whose projection then does not read the column. Two exist today. Add a third and forget to
/// declare it, and this fails with a message saying what to add.</para>
///
/// <para>A field gated in some other way would still slip past — which is the argument for
/// keeping to the convention rather than an argument against the test.</para>
/// </summary>
public class WithheldFieldCatalogTests
{
    /// <summary>The naming convention the guard hangs off.</summary>
    private const string GatePrefix = "CallerMaySee";

    /// <summary>
    /// Only the declarations that claim a hidden column.
    ///
    /// <para>The two checks either side of this hang off the <c>CallerMaySee*</c> convention,
    /// which is a statement about controllers. A <c>Rows</c> or <c>Action</c> declaration is
    /// enforced in a handler and names one — requiring it to point at a controller property
    /// would be requiring it to lie. Those are covered by
    /// <see cref="EveryDeclaration_IsUsable"/>, which checks every entry says where it is
    /// enforced, and by the unit tests over the rules themselves.</para>
    /// </summary>
    private static List<WithheldFieldDto> FieldEntries() =>
        WithheldFieldCatalog.Entries
            .Where(entry => entry.Kind == WithheldKind.Field)
            .ToList();

    private static IEnumerable<(string Controller, string Property)> RoleGates() =>
        typeof(Program).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .SelectMany(type => type
                .GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly)
                .Where(property =>
                    property.PropertyType == typeof(bool)
                    && property.Name.StartsWith(GatePrefix, StringComparison.Ordinal))
                .Select(property => (
                    Controller: type.Name.Replace("Controller", string.Empty),
                    Property: property.Name)));

    [Fact]
    public void EveryRoleGatedField_IsDeclaredInTheCatalogue()
    {
        var gates = RoleGates().ToList();

        gates.Should().NotBeEmpty(
            $"the {GatePrefix}* convention is how a controller withholds a field by role; if "
            + "this is empty the convention has been abandoned and this guard no longer "
            + "protects anything");

        var undeclared = gates
            .Where(gate => !FieldEntries().Any(entry =>
                entry.EnforcedIn.Contains(gate.Property, StringComparison.Ordinal)))
            .Select(gate => $"{gate.Controller}Controller.{gate.Property}")
            .ToList();

        undeclared.Should().BeEmpty(
            "a field withheld from a role has to be declared in WithheldFieldCatalog, or the "
            + "platform access page shows the endpoint as fully readable by that role and says "
            + "nothing about what is missing from the response. Add an entry naming the "
            + "property above in EnforcedIn.");
    }

    /// <summary>
    /// The other direction: a declaration naming a property that no longer exists.
    ///
    /// <para>Worse than a missing entry, because it reads as a live restriction. If the gate
    /// was removed — a field opened up to every role — the catalogue would still be telling an
    /// operator it is withheld.</para>
    /// </summary>
    [Fact]
    public void EveryDeclaration_NamesAGateThatStillExists()
    {
        var gates = RoleGates().Select(gate => gate.Property).ToList();

        var stale = FieldEntries()
            .Where(entry => !gates.Any(gate =>
                entry.EnforcedIn.Contains(gate, StringComparison.Ordinal)))
            .Select(entry => $"{entry.Area} / {entry.Field} → {entry.EnforcedIn}")
            .ToList();

        stale.Should().BeEmpty(
            "this entry names a controller gate that no longer exists, so either the "
            + "restriction was lifted and the entry should go, or it was renamed and the entry "
            + "should follow it");
    }

    /// <summary>
    /// A row or action restriction has to name where it is enforced in code, the same as a
    /// field does — the whole value of the page is that a claim can be checked.
    /// </summary>
    [Fact]
    public void NonFieldDeclarations_NameAHandler()
    {
        var vague = WithheldFieldCatalog.Entries
            .Where(entry => entry.Kind != WithheldKind.Field)
            .Where(entry => !entry.EnforcedIn.Contains("Handler", StringComparison.Ordinal)
                && !entry.EnforcedIn.Contains("Policy", StringComparison.Ordinal))
            .Select(entry => $"{entry.Area} / {entry.Field} → {entry.EnforcedIn}")
            .ToList();

        vague.Should().BeEmpty(
            "a row or action restriction is enforced in a handler or a policy class, and the "
            + "entry has to name it so an operator can go and read the rule rather than "
            + "trusting this table");
    }

    [Fact]
    public void EveryDeclaration_IsUsable()
    {
        WithheldFieldCatalog.Entries.Should().NotBeEmpty();

        foreach (var entry in WithheldFieldCatalog.Entries)
        {
            entry.Area.Should().NotBeNullOrWhiteSpace();
            entry.Field.Should().NotBeNullOrWhiteSpace();
            entry.WithheldFrom.Should().NotBeNullOrWhiteSpace();

            // The reason is the part a reader actually needs: a table of hidden fields with no
            // rationale invites somebody to "fix" a restriction that was deliberate.
            entry.Why.Should().NotBeNullOrWhiteSpace();

            entry.EnforcedIn.Should().NotBeNullOrWhiteSpace(
                "an entry that does not say where it is enforced cannot be checked, and an "
                + "unverifiable claim on an access page is worse than no claim");
        }
    }
}
