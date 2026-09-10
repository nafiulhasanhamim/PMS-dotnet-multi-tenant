using System.Text.Json;

namespace PMS.Web.Selection;

/// <summary>
/// The catalogue entries a person has ticked, held across requests.
///
/// <para><b>Why server session and not the page.</b> The selection has to survive paging and,
/// more importantly, a completely new search: the intended flow is to search "napa", tick
/// three, search "azin", tick two more, then continue with all five. Nothing about that
/// journey passes the earlier ticks through the form, so hidden inputs cannot carry them.</para>
///
/// <para>Rejected alternatives. A query string would have to carry up to two hundred ids and
/// would break the moment somebody followed a plain link. A cookie has a four-kilobyte ceiling
/// that two hundred ids and names would exceed, and would ship the whole selection on every
/// request including images. <c>localStorage</c> would work but moves the source of truth into
/// the browser, where the server building the next screen cannot read it.</para>
///
/// <para>Names are stored alongside the ids so the footer and the setup screen can show what
/// was chosen without re-querying the catalogue for entries the person may have searched past
/// several pages ago.</para>
/// </summary>
public sealed class CatalogSelection
{
    /// <summary>
    /// Session key. Versioned, so a shape change cannot deserialise onto an older payload and
    /// leave somebody with a selection they cannot clear.
    /// </summary>
    public const string SessionKey = "pms.catalog-selection.v1";

    /// <summary>
    /// The cap, matching the API's per-request limit. Enforced as the selection is built, so
    /// the person is told at the point of ticking rather than after filling in a grid of 250.
    /// </summary>
    public const int MaxItems = 200;

    public Dictionary<int, string> Items { get; set; } = [];

    public int Count => Items.Count;

    public bool IsFull => Items.Count >= MaxItems;

    public bool Contains(int catalogMedicineId) => Items.ContainsKey(catalogMedicineId);

    public IReadOnlyList<int> Ids => Items.Keys.ToList();

    /// <summary>Adds an entry. Returns false when the cap is already reached.</summary>
    public bool Add(int catalogMedicineId, string brandName)
    {
        if (Items.ContainsKey(catalogMedicineId))
        {
            return true;
        }

        if (IsFull)
        {
            return false;
        }

        Items[catalogMedicineId] = brandName;
        return true;
    }

    public void Remove(int catalogMedicineId) => Items.Remove(catalogMedicineId);

    public void Clear() => Items.Clear();
}

/// <summary>Reads and writes the selection in session.</summary>
public static class CatalogSelectionExtensions
{
    public static CatalogSelection GetCatalogSelection(this ISession session)
    {
        var payload = session.GetString(CatalogSelection.SessionKey);

        if (string.IsNullOrEmpty(payload))
        {
            return new CatalogSelection();
        }

        try
        {
            return JsonSerializer.Deserialize<CatalogSelection>(payload)
                ?? new CatalogSelection();
        }
        catch (JsonException)
        {
            // A payload this cannot read is worth discarding rather than throwing: the cost is
            // a lost selection, and the alternative is a person unable to load the import page
            // at all until their session expires.
            return new CatalogSelection();
        }
    }

    public static void SetCatalogSelection(this ISession session, CatalogSelection selection)
    {
        if (selection.Count == 0)
        {
            session.Remove(CatalogSelection.SessionKey);
            return;
        }

        session.SetString(CatalogSelection.SessionKey, JsonSerializer.Serialize(selection));
    }
}
