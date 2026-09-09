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
    string EnforcedIn);
