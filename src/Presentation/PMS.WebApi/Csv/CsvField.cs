using System.Globalization;

namespace PMS.WebApi.Csv;

/// <summary>
/// One CSV field, escaped per RFC 4180.
///
/// <para>Lifted out of <c>AntibioticsController</c> when the reports became a second caller. It
/// matters more than it looks: a product's brand name and a patient's name are both free text
/// typed at a counter, and an unquoted <c>Rahman, Md. Abdul</c> shifts every column after it by
/// one, for that row alone — the kind of corruption a reader notices three rows later and blames
/// on the wrong thing.</para>
/// </summary>
public static class CsvField
{
    /// <summary>Escapes a text field, quoting only when it has to.</summary>
    public static string Text(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuoting =
            value.Contains(',') || value.Contains('"') || value.Contains('\n')
            || value.Contains('\r');

        return needsQuoting
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    /// <summary>
    /// A money figure for a spreadsheet: two decimal places, invariant culture, no group
    /// separators and no currency symbol.
    ///
    /// <para>No separators, because a thousands comma inside an unquoted field is exactly the bug
    /// above. No symbol, because a spreadsheet will not sum a column it cannot read as a number —
    /// and summing this column is the whole reason somebody exported it. The taka sign belongs on
    /// the screen.</para>
    ///
    /// <para><b>This is one of the two places report figures round</b> — the screen is the other.
    /// Everything upstream aggregates unrounded; see <c>ProfitMath</c>.</para>
    /// </summary>
    public static string Money(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>A percentage to two places, bare — the column header carries the sign.</summary>
    public static string Percent(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>A count or a quantity.</summary>
    public static string Number(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A date, ISO, so it sorts as text and parses everywhere.</summary>
    public static string Date(DateOnly? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>A timestamp. UTC, like every stored time in this system.</summary>
    public static string Timestamp(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
