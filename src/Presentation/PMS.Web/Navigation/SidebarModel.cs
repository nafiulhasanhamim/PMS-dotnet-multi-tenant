namespace PMS.Web.Navigation;

/// <summary>
/// What the sidebar needs: the entries a role may see, and any counts to show beside them.
/// </summary>
/// <param name="Badges">
/// Keyed by <see cref="NavItem.BadgeKey"/>. An entry whose key is absent renders no badge, which
/// is what lets a module add a nav item before it has anything to count — and what makes a zero
/// render as nothing rather than as a reassuring "0" somebody has to read every time.
/// </param>
public sealed record SidebarModel(
    IEnumerable<NavItem> Items,
    IReadOnlyDictionary<string, int> Badges)
{
    public static SidebarModel Of(IEnumerable<NavItem> items) =>
        new(items, new Dictionary<string, int>());

    public int? BadgeFor(NavItem item) =>
        item.BadgeKey is { } key && Badges.TryGetValue(key, out var count) && count > 0
            ? count
            : null;
}
