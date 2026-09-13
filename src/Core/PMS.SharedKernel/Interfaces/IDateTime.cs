using PMS.SharedKernel.DependencyInjection;

namespace PMS.SharedKernel.Interfaces;

/// <summary>
/// The application clock. Mockable, so anything time-dependent can be tested without
/// waiting on the wall clock. Registered as a singleton.
///
/// <para><b>UTC only, deliberately.</b> Every stored timestamp in this system is UTC, and a
/// local-time property on the shared clock is how one stops being: it reads as the obviously
/// convenient choice at the call site and lands, six hours out, in a column named
/// <c>...OnUtc</c>. There was such a property here and nothing used it — which is the best
/// possible moment to remove one.</para>
///
/// <para>Local time is a question about a viewer, not about the system, so it belongs where a
/// viewer exists: the web layer converts on the way to the screen. Nothing below that layer
/// has any business knowing what zone anyone is in.</para>
/// </summary>
public interface IDateTime : ISingletonService
{
    /// <summary>
    /// Gets the current UTC date and time. <see cref="DateTime.Kind"/> is
    /// <see cref="DateTimeKind.Utc"/>.
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Gets today's date in UTC, at midnight. <see cref="DateTime.Kind"/> is
    /// <see cref="DateTimeKind.Utc"/>.
    ///
    /// <para>Named for the zone it is in, because it disagrees with a local "today" for six
    /// hours out of every twenty-four in Bangladesh, and that disagreement decides which day
    /// a sale or an expiry falls on. The property this replaces was documented as UTC and
    /// returned <see cref="DateTime.Today"/>, which is local midnight.</para>
    /// </summary>
    DateTime UtcToday { get; }
}
