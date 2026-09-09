using PMS.Domain.Enums;

namespace PMS.WebApi.Access;

/// <summary>
/// The whole access picture, for a platform operator.
/// </summary>
/// <param name="GeneratedAtUtc">
/// When it was read out of the running application. Worth showing: this is a live reflection
/// of the deployed build, not a document somebody maintains, and the timestamp is what says so.
/// </param>
public sealed record AccessMatrixDto(
    IReadOnlyList<AccessRoleDto> Roles,
    IReadOnlyList<AccessAreaDto> Areas,
    IReadOnlyList<WithheldFieldDto> WithheldFields,
    int TotalEndpoints,
    int AnonymousEndpoints,
    DateTime GeneratedAtUtc);

/// <param name="PermittedCount">
/// How many of the API's authenticated endpoints this role can call. A blunt number, and useful
/// exactly once: when it changes after a release nobody expected it to.
/// </param>
public sealed record AccessRoleDto(
    UserRole Role,
    string Name,
    string Description,
    int PermittedCount);

public sealed record AccessAreaDto(string Name, IReadOnlyList<AccessEntryDto> Entries);

/// <summary>
/// One endpoint, and who may call it.
/// </summary>
/// <param name="Policies">
/// Every policy in force, controller and action together. Two of them means the caller has to
/// satisfy <b>both</b> — see <paramref name="AllowedRoles"/>.
/// </param>
/// <param name="AllowedRoles">
/// The <b>intersection</b> of the policies' role sets, not the union. A controller marked
/// TenantUser with an action marked TenantWriter admits Admin and Pharmacist — the action
/// narrows the controller, it does not widen it. Computing this as a union would report an
/// Employee as able to add stock.
/// </param>
/// <param name="RequiresAuthentication">
/// False for the handful of endpoints that are deliberately open: signing in, and the health
/// check. Called out rather than hidden, because "which endpoints need no token at all" is a
/// question worth being able to answer in one place.
/// </param>
public sealed record AccessEntryDto(
    string Action,
    string Description,
    string Method,
    string Route,
    IReadOnlyList<string> Policies,
    bool RequiresAuthentication,
    IReadOnlyList<UserRole> AllowedRoles);

/// <summary>
/// Something withheld <em>inside</em> a response, rather than by refusing the request.
/// </summary>
/// <remarks>
/// <b>Declared, not derived — and that difference matters.</b> Everything else on this page is
/// read out of the running application's own authorization metadata, so it cannot drift. These
/// cannot be: "the projection does not read the cost column when the caller is an Employee" is
/// a fact about a handler, invisible to the routing table.
///
/// <para>So this list is maintained by hand, and it is the part of the page to distrust first.
/// Each row names the code that actually enforces it, so the claim can be checked rather than
/// taken on faith.</para>
/// </remarks>
public sealed record WithheldFieldDto(
    string Area,
    string Field,
    string WithheldFrom,
    string Why,
    string EnforcedIn);
