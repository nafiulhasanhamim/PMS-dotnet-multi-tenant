namespace PMS.Web.ViewModels;

/// <summary>
/// Wording helpers for messages that carry a count.
///
/// <para>Small, and it exists because "1 medicines added without prices" reached a screenshot.
/// Interpolating a number straight into a sentence with a hard-coded plural is right most of
/// the time and wrong at exactly the moment somebody is reading carefully — the first item
/// they add.</para>
/// </summary>
public static class Copy
{
    /// <summary>
    /// "1 medicine", "3 medicines". Pass <paramref name="plural"/> when adding an s is wrong.
    /// </summary>
    public static string Count(int count, string singular, string? plural = null) =>
        count == 1 ? $"1 {singular}" : $"{count} {plural ?? singular + "s"}";

    /// <summary>The noun alone, for sentences that put the number elsewhere.</summary>
    public static string Noun(int count, string singular, string? plural = null) =>
        count == 1 ? singular : plural ?? singular + "s";

    /// <summary>"is" / "are", agreeing with the count.</summary>
    public static string Is(int count) => count == 1 ? "is" : "are";

}
