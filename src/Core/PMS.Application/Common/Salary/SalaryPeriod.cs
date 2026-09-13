using System.Globalization;

namespace PMS.Application.Common.Salary;

/// <summary>
/// A salary month, and the handful of things every screen and validator asks about one.
///
/// <para>A month and a year travel together through this module as two loose integers because
/// that is how they are stored and how the unique index is built. The formatting and the bounds
/// check live here so that "August 2026" is spelled the same way on the generation screen, the
/// history table, the slip and every log line.</para>
/// </summary>
public static class SalaryPeriod
{
    /// <summary>
    /// The lower bound, matching the database CHECK. Not a business rule so much as a guard
    /// against a typo turning into a row nobody can find: a salary for year 202 sorts before
    /// everything and belongs to no decade anybody will look in.
    /// </summary>
    public const int MinYear = 2000;

    public const int MaxYear = 2200;

    public static bool IsValidMonth(int month) => month is >= 1 and <= 12;

    public static bool IsValidYear(int year) => year is >= MinYear and <= MaxYear;

    public static bool IsValid(int month, int year) => IsValidMonth(month) && IsValidYear(year);

    /// <summary>"August 2026", or "13/2026" if somebody got past validation.</summary>
    public static string Format(int month, int year) =>
        IsValid(month, year)
            ? new DateOnly(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture)
            : $"{month}/{year}";

    /// <summary>The first and last day of the period, both inclusive.</summary>
    public static (DateOnly From, DateOnly To) Bounds(int month, int year)
    {
        var from = new DateOnly(year, month, 1);

        return (from, from.AddMonths(1).AddDays(-1));
    }
}
