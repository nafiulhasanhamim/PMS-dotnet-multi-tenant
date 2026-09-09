using PMS.Domain.Enums;
using PMS.WebApi.Extensions;

namespace PMS.WebApi.Access;

/// <summary>
/// Which roles satisfy which authorization policy.
///
/// <para><b>This is the only place that knows.</b> The policies themselves are expressed as
/// claim requirements in <see cref="AuthenticationExtensions"/> — "has a tenant claim and a
/// role claim of Admin or Pharmacist" — which is the right way to enforce them and a useless
/// way to answer "what can a Pharmacist do?". This translates the requirements back into role
/// sets so that question has an answer.</para>
///
/// <para><b>It is a translation, not a second source of truth.</b> Nothing here grants
/// anything; the policies do the granting. If the two ever disagree, this file is the one
/// that is wrong — and <c>AccessMatrixTests</c> fails the build when a policy exists that this
/// file has not been told about, so the disagreement cannot be silent.</para>
/// </summary>
public static class RoleAccessMap
{
    /// <summary>
    /// The roles shown as columns, in the order they appear.
    ///
    /// <para>Platform admin first because it is outside every pharmacy rather than the most
    /// senior role within one — which is the distinction the whole matrix exists to make
    /// legible.</para>
    /// </summary>
    public static readonly IReadOnlyList<UserRole> Columns =
    [
        UserRole.PlatformAdmin,
        UserRole.Admin,
        UserRole.Pharmacist,
        UserRole.Employee,
    ];

    private static readonly IReadOnlySet<UserRole> PlatformOnly =
        new HashSet<UserRole> { UserRole.PlatformAdmin };

    private static readonly IReadOnlySet<UserRole> TenantAdminOnly =
        new HashSet<UserRole> { UserRole.Admin };

    private static readonly IReadOnlySet<UserRole> Writers =
        new HashSet<UserRole> { UserRole.Admin, UserRole.Pharmacist };

    private static readonly IReadOnlySet<UserRole> EveryTenantRole =
        new HashSet<UserRole> { UserRole.Admin, UserRole.Pharmacist, UserRole.Employee };

    private static readonly IReadOnlySet<UserRole> Everyone =
        new HashSet<UserRole>(Columns);

    /// <summary>
    /// The roles whose token satisfies <paramref name="policy"/>, or null when the policy is
    /// not one this map knows.
    ///
    /// <para>Note what is <em>not</em> here: a platform token never satisfies a tenant policy
    /// and a tenant token never satisfies the platform one. Each tenant policy requires a
    /// tenant claim, which a platform token does not carry — so "platform admin can do
    /// everything" is false, and the matrix shows it as false.</para>
    /// </summary>
    public static IReadOnlySet<UserRole>? RolesFor(string? policy) => policy switch
    {
        AuthenticationExtensions.PlatformAdminPolicy => PlatformOnly,
        AuthenticationExtensions.TenantAdminPolicy => TenantAdminOnly,
        AuthenticationExtensions.TenantWriterPolicy => Writers,
        AuthenticationExtensions.TenantUserPolicy => EveryTenantRole,

        // An [Authorize] with no policy means "any authenticated principal", which in this
        // API is any of the four.
        null or "" => Everyone,

        _ => null,
    };

    /// <summary>Whether this map can translate the policy. Used by the guarding test.</summary>
    public static bool IsKnown(string? policy) => RolesFor(policy) is not null;

    /// <summary>What each role is, in one line, for the legend.</summary>
    public static string Describe(UserRole role) => role switch
    {
        UserRole.PlatformAdmin =>
            "Operates the platform. Creates pharmacies and their first Admin. Belongs to no "
            + "pharmacy, so it cannot reach any pharmacy's own data.",
        UserRole.Admin => "Runs one pharmacy, including its staff.",
        UserRole.Pharmacist => "Pharmacy staff who may dispense, and who take deliveries in.",
        UserRole.Employee => "Counter staff. Can look things up; changes nothing.",
        _ => string.Empty,
    };

    /// <summary>The display name for a role column.</summary>
    public static string Name(UserRole role) => role switch
    {
        UserRole.PlatformAdmin => "Platform admin",
        UserRole.Admin => "Pharmacy admin",
        _ => role.ToString(),
    };
}
