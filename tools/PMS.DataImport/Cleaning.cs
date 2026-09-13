using System.Globalization;
using System.Text.RegularExpressions;

namespace PMS.DataImport;

/// <summary>
/// Every text and number decision the importer makes, in one place so the rules can be read
/// as a set rather than discovered one call site at a time.
/// </summary>
public static class Cleaning
{
    /// <summary>
    /// A display value: trimmed, inner whitespace collapsed, empty becomes null.
    ///
    /// Null rather than "" is deliberate. An empty string in a nullable column is a value that
    /// looks present to every query — <c>WHERE Strength IS NULL</c> misses it, and a UI shows
    /// a blank where it should show nothing at all.
    /// </summary>
    public static string? Text(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = Regex.Replace(value.Trim(), @"\s+", " ");

        return cleaned.Length == 0 ? null : cleaned;
    }

    /// <summary>
    /// The key a lookup is deduplicated on: cleaned, then lower-cased.
    ///
    /// This is what makes "Beximco", "beximco" and "BEXIMCO " one manufacturer. It is never
    /// stored — the first spelling encountered is kept for display — so the catalogue reads
    /// naturally while the dedupe stays exact. Invariant culture, because Turkish "I" folding
    /// would otherwise make the result depend on the machine running the import.
    /// </summary>
    public static string? Key(string? value) => Text(value)?.ToLowerInvariant();

    /// <summary>Truncates to a column's length, so a long outlier cannot fail the whole batch.</summary>
    public static string? Fit(string? value, int maxLength)
    {
        var cleaned = Text(value);

        return cleaned is null || cleaned.Length <= maxLength
            ? cleaned
            : cleaned[..maxLength];
    }

    // The first money amount in a pack string. The taka sign, then digits with optional
    // thousands commas and decimals: "Unit Price: ৳ 5.98,(100's pack: ৳ 598.00),".
    //
    // The FIRST match is the unit or single-container price, which is the one we want; later
    // matches are pack totals. A bare-number fallback covers any row that omits the symbol.
    private static readonly Regex TakaAmount = new(
        @"৳\s*(?<amount>[0-9][0-9,]*(?:\.[0-9]+)?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BareAmount = new(
        @"(?<amount>[0-9][0-9,]*\.[0-9]{2})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Pulls a unit price out of the raw pack text.
    ///
    /// Returns null when there is nothing to find — 42 rows are blank and 36 say
    /// "Price Unavailable", and both are legitimately "no price known" rather than zero. Zero
    /// would be a lie a pharmacy could act on.
    /// </summary>
    public static decimal? UnitPrice(string? packageContainer)
    {
        var text = Text(packageContainer);

        if (text is null)
        {
            return null;
        }

        var match = TakaAmount.Match(text);

        if (!match.Success)
        {
            match = BareAmount.Match(text);
        }

        if (!match.Success)
        {
            return null;
        }

        var digits = match.Groups["amount"].Value.Replace(",", string.Empty);

        return decimal.TryParse(digits, NumberStyles.Number, CultureInfo.InvariantCulture,
            out var price) && price >= 0
            ? price
            : null;
    }

    /// <summary>Parses the source's own row id, which the medicine upsert is keyed on.</summary>
    public static int? SourceId(string? value) =>
        int.TryParse(Text(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
        && id > 0
            ? id
            : null;
}
