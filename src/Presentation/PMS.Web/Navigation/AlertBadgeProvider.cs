using Microsoft.Extensions.Caching.Memory;
using PMS.Web.Api;
using PMS.Web.Auth;

namespace PMS.Web.Navigation;

/// <summary>
/// The counts the sidebar badge shows, cached briefly.
///
/// <para><b>Why a cache at all.</b> The layout renders on every page in the app, so without one
/// this would add an API round trip to every single request — a stock list, a login redirect, a
/// print view. A badge is a glance, not a reading: a number that is up to a minute old is worth
/// far more than a page that loads more slowly to keep it exact, and the alerts pages themselves
/// always fetch live.</para>
///
/// <para><b>Keyed per pharmacy, and that is not optional.</b> The counts are tenant data. A cache
/// keyed on nothing would show one pharmacy the other's urgent count for up to a minute, which is
/// both a leak and a lie. The key is the tenant claim from the signed-in principal.</para>
///
/// <para><b>Failure is silent by design.</b> If the API is unreachable the badge simply does not
/// appear. The alternative — a layout that throws — would take down every page in the
/// application because a decoration could not be drawn.</para>
/// </summary>
public sealed class AlertBadgeProvider
{
    /// <summary>
    /// Long enough that a burst of page loads costs one call, short enough that clearing a
    /// shelf of expired stock is reflected while the person is still looking at the screen.
    /// </summary>
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);

    private readonly PmsApiClient _api;
    private readonly IMemoryCache _cache;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<AlertBadgeProvider> _logger;

    public AlertBadgeProvider(
        PmsApiClient api,
        IMemoryCache cache,
        IHttpContextAccessor http,
        ILogger<AlertBadgeProvider> logger)
    {
        _api = api;
        _cache = cache;
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Badge counts by <see cref="NavBadges"/> key. Empty when there is nothing to show, nobody
    /// signed in, or the call failed — the layout renders no badge for an absent key.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, int>> GetAsync(CancellationToken ct = default)
    {
        var user = _http.HttpContext?.User;

        // The layout also renders for the login and access-denied pages. Asking the API for
        // alerts without a session would be a guaranteed 401 on every one of them.
        if (user?.Identity?.IsAuthenticated is not true || user.Role() is null)
        {
            return EmptyBadges;
        }

        var tenant = user.TenantDomain();

        if (string.IsNullOrEmpty(tenant))
        {
            return EmptyBadges;
        }

        var summary = await _cache.GetOrCreateAsync($"alerts:{tenant}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;

            var result = await _api.GetAlertSummaryAsync(ct);

            if (result.IsSuccess)
            {
                return result.Value;
            }

            // Cached as null for the same short window, deliberately. A pharmacy whose API is
            // briefly down should not have every page in the app retry the same failing call.
            _logger.LogWarning(
                "Alert badge unavailable for {Tenant}: {Problem}",
                tenant, result.Problem?.Message);

            return null;
        });

        if (summary is null || summary.UrgentCount <= 0)
        {
            // Zero is not a badge. A pharmacy with nothing expired and nothing out of stock
            // should see a plain nav item, not a reassuring "0" it has to read every time.
            return EmptyBadges;
        }

        return new Dictionary<string, int> { [NavBadges.Alerts] = summary.UrgentCount };
    }

    private static readonly IReadOnlyDictionary<string, int> EmptyBadges =
        new Dictionary<string, int>();
}
