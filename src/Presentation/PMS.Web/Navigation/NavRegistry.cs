using PMS.Web.Api;

namespace PMS.Web.Navigation;

/// <summary>
/// One entry in the sidebar.
/// </summary>
/// <param name="Title">Sentence case, as it appears on screen.</param>
/// <param name="Page">The Razor page path, e.g. <c>/Users/Index</c>.</param>
/// <param name="Icon">A Bootstrap-icon-style SVG key from <c>_NavIcon</c>.</param>
/// <param name="Roles">
/// Which roles see it. Empty means every signed-in user of that area.
/// </param>
/// <param name="MatchPrefix">
/// Route prefix that marks this entry active, so a child page such as <c>/users/create</c>
/// still highlights "Users". Defaults to the page's own folder.
/// </param>
/// <param name="BadgeKey">
/// Names a count the layout may render beside this entry, or null for no badge.
///
/// <para>A key rather than a number, because <see cref="NavRegistry"/> is static and a count is
/// per request. The layout looks the key up in a dictionary it builds once per page; an entry
/// whose key is absent simply renders no badge, so a module can add the item before it has
/// anything to count.</para>
/// </param>
public sealed record NavItem(
    string Title,
    string Page,
    string Icon,
    UserRole[]? Roles = null,
    string? MatchPrefix = null,
    string? BadgeKey = null)
{
    public bool IsVisibleTo(UserRole? role) =>
        Roles is null || Roles.Length == 0 || (role is not null && Roles.Contains(role.Value));

    public bool IsActive(string? currentPath)
    {
        if (string.IsNullOrEmpty(currentPath))
        {
            return false;
        }

        var prefix = MatchPrefix ?? DerivePrefix(Page);

        return prefix == "/"
            ? currentPath.Equals("/", StringComparison.OrdinalIgnoreCase)
            : currentPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string DerivePrefix(string page)
    {
        // "/Users/Index" -> "/users";  "/Index" -> "/";  "/Account" -> "/account"
        var trimmed = page.EndsWith("/Index", StringComparison.OrdinalIgnoreCase)
            ? page[..^"/Index".Length]
            : page;

        return string.IsNullOrEmpty(trimmed) ? "/" : trimmed.ToLowerInvariant();
    }
}

/// <summary>
/// The sidebar's contents, in one list per area.
///
/// **This is the file a new module edits.** Add a <see cref="NavItem"/> here and the sidebar
/// picks it up — no touching <c>_Layout.cshtml</c> or <c>_SidebarNav.cshtml</c>, which is the
/// point: shared markup that every module has to edit is shared markup that every module can
/// break, and role visibility would end up expressed differently in each place.
///
/// Visibility here is a convenience, never a security boundary. A hidden entry is a link the
/// person does not see, not a page they cannot reach — every page carries its own
/// <c>[Authorize]</c>, and that is what actually stops them.
/// </summary>
/// <summary>
/// The badge keys a nav item may carry. Constants rather than loose strings, so a typo is a
/// build error instead of a badge that silently never appears.
/// </summary>
public static class NavBadges
{
    public const string Alerts = "alerts";
}

public static class NavRegistry
{
    /// <summary>Sidebar for a signed-in pharmacy user, filtered by their role there.</summary>
    public static readonly IReadOnlyList<NavItem> Tenant = new[]
    {
        new NavItem("Dashboard", "/Index", "grid", MatchPrefix: "/"),

        // Module 5. First after the dashboard and deliberately so: it is the screen used more
        // than every other one put together, and a cashier should not have to look for it.
        // Every role sells — refusing an Employee the till would leave the pharmacy unable to
        // staff a counter — and what they cannot do is enforced inside the sale, not by hiding
        // the link.
        new NavItem("New sale", "/Billing/Index", "cart", MatchPrefix: "/billing"),

        new NavItem("Sales", "/Sales/Index", "receipt", MatchPrefix: "/sales"),

        // Module 2. Two entries, one Product table: each reads it through a type filter.
        // Split because the columns that matter differ completely — a diaper has no generic
        // name, strength, dosage form or antibiotic flag, and showing those columns empty on
        // every row is noise. Every role sees both; Employees just cannot change anything.
        new NavItem("Medicines", "/Medicines/Index", "pill", MatchPrefix: "/medicines"),

        new NavItem("Other items", "/OtherItems/Index", "box", MatchPrefix: "/other-items"),

        // Module 3. One entry, visible to every role: an Employee has to be able to answer
        // "have we got any?" without being able to change the answer. What they do not see is
        // what the stock cost, and the API withholds that rather than the page hiding it.
        new NavItem("Stock", "/Stock/Index", "layers", MatchPrefix: "/stock"),

        // Module 6. Every role sees it: an Employee cannot clear expired stock, but knowing
        // not to reach for it is exactly what counter staff should be told. The badge is what
        // makes the feature get used rather than remembered — a number beside the word is
        // noticed, and a page nobody opens is a page that may as well not exist.
        new NavItem("Alerts", "/Alerts/Index", "alert", MatchPrefix: "/alerts",
            BadgeKey: NavBadges.Alerts),

        // Module 7. Admin and Pharmacist, and the asymmetry with the till is deliberate: an
        // Employee may be able to *sell* an antibiotic depending on the pharmacy's mode, but the
        // register is the regulatory record of what colleagues dispensed and to which named
        // patients. Selling is counter work; reading that back is oversight.
        new NavItem("Antibiotic register", "/Antibiotics/Register", "clipboard",
            Roles: new[] { UserRole.Admin, UserRole.Pharmacist },
            MatchPrefix: "/antibiotics"),

        // Module 8. Admin only, and this is the strictest entry in the sidebar. Other modules
        // separate reading from writing; here the reading IS the sensitive act - these screens
        // show what stock cost, what the business earns on it, and a per-cashier discount
        // breakdown that is in effect a staff review. A Pharmacist who may dispense a controlled
        // drug still has no business knowing the owner's margin on it.
        new NavItem("Reports", "/Reports/Index", "chart",
            Roles: new[] { UserRole.Admin },
            MatchPrefix: "/reports"),

        new NavItem("Users", "/Users/Index", "people",
            Roles: new[] { UserRole.Admin }),

        new NavItem("My profile", "/Account", "person"),

        // Module 4 onwards: suppliers and purchases, then salary.
    };

    /// <summary>Sidebar for a platform operator. An entirely separate list — no overlap.</summary>
    public static readonly IReadOnlyList<NavItem> Platform = new[]
    {
        new NavItem("Tenants", "/Platform/Tenants/Index", "building",
            MatchPrefix: "/platform/tenants"),

        // Who can do what, read out of the running build. Platform-side only: a pharmacy
        // Admin has no use for the whole authorization model, and it is exactly the map
        // somebody probing the system would want.
        new NavItem("Roles & access", "/Platform/Access", "shield",
            MatchPrefix: "/platform/access"),
    };

    public static IEnumerable<NavItem> ForTenantRole(UserRole? role) =>
        Tenant.Where(item => item.IsVisibleTo(role));
}
