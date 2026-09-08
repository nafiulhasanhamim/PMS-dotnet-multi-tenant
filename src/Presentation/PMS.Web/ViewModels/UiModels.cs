using PMS.Web.Api;

namespace PMS.Web.ViewModels;

public enum AlertKind
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Danger = 3,
}

/// <summary>A banner. Colour is never the only signal — the partial adds an icon and a
/// screen-reader label, and the message itself says what happened.</summary>
public sealed record AlertModel(string? Message, AlertKind Kind, bool Dismissible = false)
{
    public static AlertModel Success(string? message) =>
        new(message, AlertKind.Success, Dismissible: true);

    public static AlertModel Danger(string? message) => new(message, AlertKind.Danger);

    public static AlertModel Warning(string? message) => new(message, AlertKind.Warning);

    public static AlertModel Info(string? message) => new(message, AlertKind.Info);
}

/// <summary>What an empty table says, and what to do about it.</summary>
public sealed record EmptyStateModel(
    string Title,
    string? Description = null,
    string? ActionText = null,
    string? ActionPage = null,
    IDictionary<string, string>? ActionRoute = null);

/// <summary>
/// Everything the pagination footer needs. Built from a <see cref="PagedView{T}"/> plus the
/// page it links back to, so the partial never has to know what is being listed.
/// </summary>
public sealed class PaginationModel
{
    public PaginationModel(
        string page,
        int currentPage,
        int totalPages,
        int totalCount,
        int firstRow,
        int lastRow,
        IDictionary<string, string>? extraRoute = null)
    {
        Page = page;
        CurrentPage = currentPage;
        TotalPages = totalPages;
        TotalCount = totalCount;
        FirstRow = firstRow;
        LastRow = lastRow;
        ExtraRoute = extraRoute;
    }

    public string Page { get; }

    public int CurrentPage { get; }

    public int TotalPages { get; }

    public int TotalCount { get; }

    public int FirstRow { get; }

    public int LastRow { get; }

    /// <summary>Route values to preserve across page links — filters, ids, and so on.</summary>
    public IDictionary<string, string>? ExtraRoute { get; }

    public bool HasPrevious => CurrentPage > 1;

    public bool HasNext => CurrentPage < TotalPages;

    public static PaginationModel For<T>(
        PagedView<T> view, string page, IDictionary<string, string>? extraRoute = null) =>
        new(page, view.Page, view.TotalPages, view.TotalCount, view.FirstRow, view.LastRow,
            extraRoute);

    /// <summary>Route values for a given page number, keeping any extras intact.</summary>
    public IDictionary<string, string> RouteFor(int pageNumber)
    {
        var route = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (ExtraRoute is not null)
        {
            foreach (var (key, value) in ExtraRoute)
            {
                route[key] = value;
            }
        }

        route["p"] = Math.Clamp(pageNumber, 1, Math.Max(TotalPages, 1)).ToString();

        return route;
    }
}

/// <summary>How a <see cref="TenantStatus"/> is shown: a badge class and its wording.</summary>
public static class StatusPresentation
{
    public static (string Css, string Text) ForTenant(TenantStatus status) => status switch
    {
        TenantStatus.Active => ("pms-badge--success", "Active"),
        TenantStatus.Trial => ("pms-badge--warning", "Trial"),
        TenantStatus.Suspended => ("pms-badge--danger", "Suspended"),
        _ => ("pms-badge--neutral", status.ToString()),
    };

    public static (string Css, string Text) ForMembership(bool isActive) =>
        isActive
            ? ("pms-badge--success", "Active")
            : ("pms-badge--danger", "Inactive");

    public static (string Css, string Text) ForRole(UserRole role) => role switch
    {
        UserRole.PlatformAdmin => ("pms-badge--platform", "Platform admin"),
        UserRole.Admin => ("pms-badge--info", "Admin"),
        UserRole.Pharmacist => ("pms-badge--neutral", "Pharmacist"),
        UserRole.Employee => ("pms-badge--neutral", "Employee"),
        _ => ("pms-badge--neutral", role.ToString()),
    };
}
