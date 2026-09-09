namespace PMS.SharedKernel.Interfaces;

/// <summary>
/// Conveniences over the application clock.
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Today, in UTC, as a <see cref="DateOnly"/>.
    ///
    /// <para>Expiry dates are days, not instants — a pack prints a month and a year, and
    /// nothing about it happens at a particular time — so the stock module compares
    /// <see cref="DateOnly"/> throughout. Converting here, once, keeps
    /// <c>DateOnly.FromDateTime</c> out of a dozen call sites, each of which would be one
    /// opportunity to reach for <c>DateTime.Today</c> instead and land back on local
    /// midnight.</para>
    /// </summary>
    public static DateOnly UtcDateToday(this IDateTime clock)
        => DateOnly.FromDateTime(clock.UtcNow);
}
