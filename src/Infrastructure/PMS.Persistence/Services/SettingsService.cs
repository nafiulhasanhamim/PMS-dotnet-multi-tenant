using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.Settings;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Persistence.Contexts;

namespace PMS.Persistence.Services;

/// <summary>
/// Every configurable value this pharmacy has. See <see cref="ISettingsService"/>.
///
/// <para><b>Registered scoped, and the scope is the cache.</b> One instance serves one request
/// and the dictionary below holds the answer for its duration. A sale asks for the discount cap
/// once per line and the antibiotic mode once per cart item; without this a ten-item cart would
/// be twenty identical queries about values that cannot change mid-request.</para>
///
/// <para><b>Nothing is cached across requests, deliberately.</b> A short-lived process cache
/// would be marginally faster and wrong in the way that matters: an Admin who raises a discount
/// cap and then watches a cashier get refused has no way to tell a stale cache from a bug.
/// Reading once per request means a saved change is in force on the very next one — a stronger
/// guarantee than the "within seconds" that was asked for, and for ten rows read from a covering
/// index it costs nothing worth optimising.</para>
///
/// <para><b>A missing key falls back and logs a warning.</b> Missing keys mean a seeding gap, not
/// a configuration choice, so the log line is the point: the fallback keeps billing working while
/// the warning says the seed needs looking at. Throwing instead would take down a till because a
/// row was absent.</para>
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SettingsService> _logger;

    private Dictionary<string, string>? _cache;

    public SettingsService(ApplicationDbContext context, ILogger<SettingsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> GetStringAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var values = await LoadAsync(cancellationToken);

        if (values.TryGetValue(key, out var value))
        {
            return value;
        }

        return Fallback(key, "missing");
    }

    /// <inheritdoc />
    public async Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default)
    {
        var raw = await GetStringAsync(key, cancellationToken);

        if (int.TryParse(raw, out var parsed))
        {
            return parsed;
        }

        // Unparseable as well as missing. A row reading "ninety" is a hand-editing accident, and
        // the ninety-day default is a better answer than a FormatException surfacing from inside
        // an alerts query on somebody's dashboard.
        var fallback = Fallback(key, $"unparseable value '{Trim(raw)}'");

        return int.TryParse(fallback, out var defaulted) ? defaulted : 0;
    }

    /// <inheritdoc />
    public async Task<TEnum> GetEnumAsync<TEnum>(
        string key, CancellationToken cancellationToken = default)
        where TEnum : struct, Enum
    {
        var raw = await GetStringAsync(key, cancellationToken);

        if (Enum.TryParse<TEnum>(raw, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        var fallback = Fallback(key, $"unrecognised value '{Trim(raw)}'");

        return Enum.TryParse<TEnum>(fallback, ignoreCase: true, out var defaulted)
            ? defaulted
            : default;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var stored = await LoadAsync(cancellationToken);

        // Defaults filled in for anything absent, so the settings screen renders a complete form
        // against a partially seeded pharmacy rather than a page of blanks.
        var complete = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var definition in SettingKeys.All)
        {
            complete[definition.Key] = stored.TryGetValue(definition.Key, out var value)
                ? value
                : definition.Default;
        }

        return complete;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>> SaveAsync(
        IReadOnlyDictionary<string, string> values,
        Guid? updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (values.Count == 0)
        {
            return [];
        }

        var keys = values.Keys.ToList();

        // Tracked, because these are being written. The tenant filter scopes this to the
        // pharmacy whose request it is, which is what stops an Admin at one pharmacy writing
        // another's rules even by crafting the request.
        var existing = await _context.AppSettings
            .Where(s => keys.Contains(s.Key))
            .ToListAsync(cancellationToken);

        var byKey = existing.ToDictionary(s => s.Key, StringComparer.Ordinal);
        var changed = new List<string>();

        foreach (var (key, value) in values)
        {
            if (byKey.TryGetValue(key, out var row))
            {
                // Skipped rather than rewritten when nothing moved, so "who last changed the
                // discount cap" survives somebody saving the page having edited only the phone.
                if (string.Equals(row.Value, value, StringComparison.Ordinal))
                {
                    continue;
                }

                row.Update(value, updatedByUserId);
            }
            else
            {
                // A pharmacy whose seed never ran repairs itself here, the first time an Admin
                // saves the page - rather than staying silently on fallbacks forever.
                _logger.LogInformation(
                    "Setting '{Key}' did not exist for this pharmacy and is being created. "
                    + "This means the seed was incomplete.", key);

                await _context.AppSettings.AddAsync(
                    new AppSetting(key, value, updatedByUserId), cancellationToken);
            }

            changed.Add(key);
        }

        if (changed.Count == 0)
        {
            return [];
        }

        // One SaveChanges for the whole set. EF wraps it in a transaction, so a request that
        // fails partway leaves nothing behind.
        await _context.SaveChangesAsync(cancellationToken);

        Invalidate();

        return changed;
    }

    /// <inheritdoc />
    public void Invalidate() => _cache = null;

    /// <summary>
    /// The one read. Every accessor above comes through here, so asking for the pharmacy name and
    /// the discount cap in the same request is one query rather than two.
    /// </summary>
    /// <remarks>
    /// No tenant id appears anywhere: <c>AppSetting</c> is an <c>ITenantEntity</c>, so the global
    /// query filter scopes this to the pharmacy whose request it is. That is also what makes an
    /// unresolved tenant return nothing and therefore fall back to defaults, which is the safe
    /// behaviour for a request that should not have got this far.
    /// </remarks>
    private async Task<Dictionary<string, string>> LoadAsync(CancellationToken cancellationToken)
    {
        if (_cache is { } cached)
        {
            return cached;
        }

        var rows = await _context.AppSettings
            .AsNoTracking()
            .Select(s => new { s.Key, s.Value })
            .ToListAsync(cancellationToken);

        _cache = rows.ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal);

        return _cache;
    }

    /// <summary>
    /// The value this setting had as a hardcoded constant before Module 10, plus a warning saying
    /// why we are reading it.
    /// </summary>
    private string Fallback(string key, string reason)
    {
        var definition = SettingKeys.Find(key);

        if (definition is null)
        {
            // Asking for a key nothing declares is a programming error rather than a seeding gap,
            // and there is no sensible value to invent - so this one is loud and empty.
            _logger.LogError(
                "Setting '{Key}' is not declared in SettingKeys; returning empty. This is a bug, "
                + "not a configuration problem.", key);

            return string.Empty;
        }

        _logger.LogWarning(
            "Setting '{Key}' is {Reason} for this pharmacy; falling back to '{Default}'. "
            + "This indicates a seeding gap - check that migration 016 ran and that tenant "
            + "creation seeds settings.",
            key, reason, definition.Default);

        return definition.Default;
    }

    private static string Trim(string value) =>
        value.Length <= 40 ? value : value[..40] + "...";
}
