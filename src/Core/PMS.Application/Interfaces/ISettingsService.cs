using PMS.Application.Common.Settings;

namespace PMS.Application.Interfaces;

/// <summary>
/// Every configurable value this pharmacy has, typed.
///
/// <para><b>This interface is the whole point of Module 10.</b> Before it, the expiry window, the
/// dead-stock threshold, the two discount caps and the default reorder level were constants in
/// five different files, each with a comment promising a settings module. A settings screen that
/// saved values nothing read would have been worse than none, because it would have lied. Every
/// one of those constants now comes from here.</para>
///
/// <para><b>Casting lives here and nowhere else.</b> Values are stored as text — see
/// <c>AppSetting</c> for why — and a second place that parsed one would be the beginning of two
/// pharmacies behaving differently for reasons nobody could find. Call sites ask for an
/// <c>int</c> and get an <c>int</c>.</para>
///
/// <para><b>Caching: one read per request, cached for the life of that request.</b> The
/// implementation is registered scoped. A sale asks for the discount cap once per line and the
/// antibiotic mode once per cart item; without the cache a ten-item cart would be twenty
/// identical queries about values that cannot change mid-request.</para>
///
/// <para>Nothing is cached <em>across</em> requests, deliberately. A five-minute cache would be
/// marginally faster and wrong in the way that matters: an Admin who raises a discount cap and
/// then watches a cashier get refused would have no way to tell a stale cache from a bug. Reading
/// once per request means a saved change is in force on the very next one — which is a stronger
/// guarantee than the brief's "within seconds", for a table of ten rows.</para>
///
/// <para><b>A missing key falls back to the value it had as a hardcoded constant</b>, and logs a
/// warning. Missing keys mean a seeding gap — a tenant created before this migration, or a seed
/// that failed halfway — and the alternative, throwing, would take down billing because a row was
/// absent. See <see cref="SettingKeys"/>.</para>
/// </summary>
public interface ISettingsService
{
    /// <summary>The value as text. Empty is a legitimate answer for an optional setting.</summary>
    Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// The value as a whole number.
    ///
    /// <para>Falls back on an unparseable value as well as a missing one. A row reading "ninety"
    /// is a seeding or hand-editing accident, and the ninety-day default is a better answer than
    /// a <c>FormatException</c> surfacing from inside an alerts query.</para>
    /// </summary>
    Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>The value as one of <typeparamref name="TEnum"/>'s names, case-insensitively.</summary>
    Task<TEnum> GetEnumAsync<TEnum>(string key, CancellationToken cancellationToken = default)
        where TEnum : struct, Enum;

    /// <summary>
    /// Every setting, keyed by name, with defaults filled in for anything missing.
    ///
    /// <para>What the settings screen and <c>GET /api/settings</c> read. Returning the whole set
    /// in one go is also what makes the dashboard a single round trip rather than one query per
    /// figure it needs a threshold for.</para>
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a set of settings, creating any row that does not exist yet.
    ///
    /// <para><b>All of them or none.</b> One <c>SaveChanges</c>, so a request carrying a valid
    /// pharmacy name and an invalid discount cap changes nothing — partial application would
    /// leave a pharmacy in a state nobody asked for and no screen would show as incomplete.
    /// Validation happens before this is called; by the time it runs, every value is known
    /// good.</para>
    ///
    /// <para>Unchanged keys are skipped rather than rewritten, so "who last changed the discount
    /// cap" keeps meaning what it says after somebody saves the page having edited only the
    /// pharmacy phone.</para>
    ///
    /// <para>Creating missing rows matters as much as updating present ones: it is what lets a
    /// pharmacy whose seed never ran repair itself the first time an Admin saves the settings
    /// page, rather than staying silently on fallbacks forever.</para>
    ///
    /// <para>Invalidates the cache, so a read later in the same request sees the new values.</para>
    /// </summary>
    /// <returns>The keys whose values actually changed.</returns>
    Task<IReadOnlyCollection<string>> SaveAsync(
        IReadOnlyDictionary<string, string> values,
        Guid? updatedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the cached read, so the next ask goes back to the database.
    ///
    /// <para><see cref="SaveAsync"/> calls this for you. It is public because a request that
    /// writes settings through some other path — the tenant-creation seed, a test — would
    /// otherwise keep serving the values it read before the write.</para>
    /// </summary>
    void Invalidate();
}
