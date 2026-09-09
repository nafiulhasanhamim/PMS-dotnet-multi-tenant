using Microsoft.AspNetCore.Mvc;
using PMS.Web.Api;

namespace PMS.Web.Pages.Platform;

/// <summary>
/// Who can do what, across the whole platform.
///
/// <para>Authorized by the <c>/Platform</c> folder convention, so platform operators only. A
/// pharmacy Admin has no use for it — their own staff page already shows the three roles they
/// can hand out — and a complete map of the authorization model is exactly what somebody
/// probing the system would like to have.</para>
///
/// <para>Inherits <see cref="PlatformPageModel"/> so a failure clears the platform cookie
/// rather than the tenant one.</para>
/// </summary>
public class AccessModel : PlatformPageModel
{
    private readonly PmsApiClient _api;

    public AccessModel(PmsApiClient api)
    {
        _api = api;
    }

    /// <summary>Show one role only. Answers "what can a Pharmacist reach?" directly.</summary>
    [BindProperty(SupportsGet = true, Name = "role")]
    public UserRole? Role { get; set; }

    /// <summary>Narrow to one area of the API.</summary>
    [BindProperty(SupportsGet = true, Name = "area")]
    public string? Area { get; set; }

    /// <summary>Hide the endpoints that need no token at all, which are mostly noise.</summary>
    [BindProperty(SupportsGet = true, Name = "auth")]
    public bool AuthenticatedOnly { get; set; } = true;

    public AccessMatrix Matrix { get; private set; } = AccessMatrix.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await _api.GetAccessMatrixAsync(ct);

        if (!result.IsSuccess)
        {
            return await HandleFailureAsync(result) ?? Page();
        }

        Matrix = result.Value ?? AccessMatrix.Empty;

        return Page();
    }

    /// <summary>Every area name, for the filter.</summary>
    public IReadOnlyList<string> AreaNames =>
        Matrix.Areas.Select(a => a.Name).ToList();

    /// <summary>
    /// The areas after filtering, with empty areas dropped.
    ///
    /// <para>Filtering by role keeps only the rows that role can reach, which is what makes the
    /// role filter answer a question rather than just dim some ticks.</para>
    /// </summary>
    public IReadOnlyList<AccessArea> FilteredAreas =>
        Matrix.Areas
            .Where(area => Area is null || area.Name == Area)
            .Select(area => new AccessArea(
                area.Name,
                area.Entries
                    .Where(entry => !AuthenticatedOnly || entry.RequiresAuthentication)
                    .Where(entry => Role is null || entry.Allows(Role.Value))
                    .ToList()))
            .Where(area => area.Entries.Count > 0)
            .ToList();

    /// <summary>The role columns actually rendered: all of them, or just the filtered one.</summary>
    public IReadOnlyList<AccessRole> Columns =>
        Role is null
            ? Matrix.Roles
            : Matrix.Roles.Where(r => r.Role == Role.Value).ToList();

    public int ShownCount => FilteredAreas.Sum(area => area.Entries.Count);

    public bool HasFilters =>
        Role is not null || Area is not null || !AuthenticatedOnly;

    /// <summary>The current filters, so links keep them.</summary>
    public Dictionary<string, string> RouteValues => new()
    {
        ["role"] = Role?.ToString() ?? string.Empty,
        ["area"] = Area ?? string.Empty,
        ["auth"] = AuthenticatedOnly.ToString(),
    };
}
