namespace PMS.Application.Common.Products;

/// <summary>
/// What identifies a product within one pharmacy: <b>brand name, strength and dosage
/// form</b>.
///
/// <para><b>Why dosage form is part of it.</b> It was not, until the bulk import made the
/// consequence obvious. The reference catalogue holds 548 brand+strength groups with more than
/// one entry — Nyclobate 0.05% exists six times, as a lotion, a spray, a shampoo, a scalp
/// solution, an ointment and a cream. A pharmacy stocks several of those, at different prices,
/// and under the old identity it could hold exactly one of them.</para>
///
/// <para><b>One definition, used everywhere.</b> The database materialises the same expression
/// as a persisted computed column and puts a single unique index over it — see migration 010.
/// The in-memory checks here and the constraint there therefore agree by construction, not by
/// coincidence: two implementations would eventually differ over a trailing space and report a
/// clash the constraint does not, or miss one it does.</para>
/// </summary>
public static class ProductKeys
{
    /// <summary>
    /// Separator between the parts. A unit separator cannot appear in a brand name, a strength
    /// or a dosage form, so "Napa" + "500 mg" cannot collide with "Napa 500" + "mg". A space
    /// or a pipe could appear in any of the three.
    ///
    /// <para>The database uses <c>CHAR(31)</c>, which is the same character.</para>
    /// </summary>
    private const char Separator = '';

    /// <summary>
    /// The identity key for a product, lower-cased and trimmed.
    ///
    /// <para>Matching how SQL Server's default collation compares the columns —
    /// <c>SQL_Latin1_General_CP1_CI_AS</c> is case-insensitive, so the comparison here has to
    /// be too. <c>ToLowerInvariant</c>, not <c>ToLower</c>: a Turkish locale lower-cases I to
    /// a dotless ı, and whether two products collide must not depend on where the server
    /// happens to be running.</para>
    /// </summary>
    public static string Identity(string brandName, string? strength, string? dosageForm) =>
        string.Join(
            Separator,
            brandName.Trim().ToLowerInvariant(),
            Normalise(strength),
            Normalise(dosageForm));

    /// <summary>
    /// How the identity reads in a message: "Napa 500 at 500 mg (Tablet)".
    ///
    /// <para>Built here so every conflict message names the same three things. A message that
    /// mentioned only brand and strength would leave somebody staring at two rows that plainly
    /// differ and no explanation of why one was refused — which is what happened before dosage
    /// form was part of the identity.</para>
    /// </summary>
    public static string Describe(string brandName, string? strength, string? dosageForm)
    {
        var text = $"'{brandName.Trim()}'";

        if (!string.IsNullOrWhiteSpace(strength))
        {
            text += $" at {strength.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(dosageForm))
        {
            text += $" ({dosageForm.Trim()})";
        }

        return text;
    }

    /// <summary>Absent parts collapse to empty, as <c>ISNULL</c> does in the computed column.</summary>
    private static string Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
}
