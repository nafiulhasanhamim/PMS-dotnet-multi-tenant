namespace PMS.Web.Api;

// ═══════════════════════════════════════════════════════════════════════════════════════════
// The platform access matrix, as the API sends it.
//
// UserRole already exists in ApiContracts (Module 1), so it is not redeclared here.
// ═══════════════════════════════════════════════════════════════════════════════════════════

/// <param name="GeneratedAtUtc">
/// When the API read this out of its own routing table. Shown on the page, because the value of
/// this screen is that it reflects the deployed build rather than a document somebody keeps up
/// to date — and the timestamp is what tells the reader that.
/// </param>
public sealed record AccessMatrix(
    IReadOnlyList<AccessRole> Roles,
    IReadOnlyList<AccessArea> Areas,
    IReadOnlyList<WithheldField> WithheldFields,
    int TotalEndpoints,
    int AnonymousEndpoints,
    DateTime GeneratedAtUtc)
{
    public static AccessMatrix Empty { get; } = new([], [], [], 0, 0, default);
}

public sealed record AccessRole(
    UserRole Role,
    string Name,
    string Description,
    int PermittedCount);

public sealed record AccessArea(string Name, IReadOnlyList<AccessEntry> Entries);

/// <param name="AllowedRoles">
/// The intersection of every policy in force, not the union — an action marked TenantWriter
/// inside a controller marked TenantUser narrows to Admin and Pharmacist.
/// </param>
public sealed record AccessEntry(
    string Action,
    string Description,
    string Method,
    string Route,
    IReadOnlyList<string> Policies,
    bool RequiresAuthentication,
    IReadOnlyList<UserRole> AllowedRoles)
{
    public bool Allows(UserRole role) => AllowedRoles.Contains(role);
}

/// <summary>
/// Something withheld inside a response rather than by refusing the request. <b>Declared by
/// hand</b>, unlike everything else here — the page says so where it renders them.
/// </summary>
public sealed record WithheldField(
    string Area,
    string Field,
    string WithheldFrom,
    string Why,
    string EnforcedIn,
    WithheldKind Kind = WithheldKind.Field);

/// <summary>
/// What shape of restriction a declaration describes. Numeric values must match the server's.
///
/// <para>Conflating the three is how an access review goes wrong: a hidden column, a shorter
/// list and a refused request need different things checked, and a table that called all three
/// "withheld field" would leave a reader believing Module 5's sales list returns everybody's
/// rows with some columns blanked, which is not what happens.</para>
/// </summary>
public enum WithheldKind
{
    Field = 0,
    Rows = 1,
    Action = 2,
}
