namespace PMS.Web.Tenancy;

/// <summary>
/// How a pharmacy is recognised from the address in the browser.
/// </summary>
public sealed class TenancyOptions
{
    public const string SectionName = "Tenancy";

    /// <summary>
    /// The address you own, with pharmacies sitting underneath it as a prefix:
    /// <c>popular-pharmacy.pms.example.com</c> for a base domain of <c>pms.example.com</c>.
    ///
    /// Set once. Every pharmacy created afterwards works immediately, with no per-pharmacy
    /// setup, which is the whole reason for choosing this shape over giving each pharmacy its
    /// own separate address.
    /// </summary>
    public string BaseDomain { get; set; } = "localhost";

    /// <summary>
    /// Prefixes that are the application itself rather than a pharmacy.
    ///
    /// Without this, visiting <c>www.pms.example.com</c> would try to sign you in to a
    /// pharmacy called "www".
    /// </summary>
    public string[] ReservedLabels { get; set; } = ["www", "admin", "platform", "api", "app"];
}

/// <summary>Reads the pharmacy out of the request's host, if it carries one.</summary>
public interface ITenantHostResolver
{
    /// <summary>
    /// The host to identify the pharmacy by, or null when the address names no pharmacy —
    /// the bare base domain, a reserved prefix, or nothing usable.
    /// </summary>
    string? Resolve(HttpRequest request);

    /// <summary>How that host should be shown to a person.</summary>
    string Display(string host);

    /// <summary>The address a pharmacy's staff should be given.</summary>
    string SignInUrl(string domainName, bool secure, int? port = null);

    /// <summary>The platform's own address, normalised, or null when none is configured.</summary>
    string? BaseDomain { get; }
}

public sealed class TenantHostResolver : ITenantHostResolver
{
    private readonly TenancyOptions _options;

    public TenantHostResolver(TenancyOptions options)
    {
        _options = options;
    }

    public string? BaseDomain
    {
        get
        {
            var value = _options.BaseDomain?.Trim().Trim('.').ToLowerInvariant();

            return string.IsNullOrEmpty(value) ? null : value;
        }
    }

    public string? Resolve(HttpRequest request)
    {
        // Returns the host itself, not a stripped label.
        //
        // Deciding which pharmacy a host belongs to is the API's job — it owns the tenant
        // rows, and a pharmacy may be named either by a prefix under the base domain or by an
        // address it owns outright. Working that out here would mean this app guessing at
        // data it cannot see, and getting it wrong for every pharmacy with its own domain.
        // All this has to answer is the narrower question: does this address name a pharmacy
        // at all, or is it the platform's own front door?
        //
        // Request.Host, not X-Forwarded-Host or anything else a caller can bolt on. The host
        // is still client-supplied — but it only ever *selects* a pharmacy to attempt, and the
        // login behind it still demands valid credentials and a membership there. The worst a
        // forged host achieves is the same generic failure as typing a wrong domain by hand.
        var host = request.Host.Host;

        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        host = host.Trim().TrimEnd('.').ToLowerInvariant();

        var baseDomain = _options.BaseDomain?.Trim().Trim('.').ToLowerInvariant();

        if (string.IsNullOrEmpty(baseDomain))
        {
            return null;
        }

        // The base domain on its own is the app's front door, not a pharmacy.
        if (host == baseDomain)
        {
            return null;
        }

        var suffix = "." + baseDomain;

        if (!host.EndsWith(suffix, StringComparison.Ordinal))
        {
            // Some other address entirely — which is exactly what a pharmacy that owns its
            // own domain looks like. Hand it over; the API knows whether one claims it.
            return host;
        }

        var label = host[..^suffix.Length];

        // Reserved prefixes are the application itself. Without this, visiting
        // www.pms.example.com would offer to sign you in to a pharmacy called "www".
        if (label.Length > 0
            && !label.Contains('.')
            && _options.ReservedLabels.Contains(label, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        return host;
    }

    /// <summary>
    /// The part of a host worth showing a person: the prefix when there is one, otherwise the
    /// whole address. "popular-pharmacy" reads better than
    /// "popular-pharmacy.pms.example.com", while "citycare.com" has to stay as it is.
    /// </summary>
    public string Display(string host)
    {
        var baseDomain = _options.BaseDomain?.Trim().Trim('.').ToLowerInvariant();

        if (string.IsNullOrEmpty(baseDomain))
        {
            return host;
        }

        var suffix = "." + baseDomain;

        if (!host.EndsWith(suffix, StringComparison.Ordinal))
        {
            return host;
        }

        var label = host[..^suffix.Length];

        return label.Length > 0 && !label.Contains('.') ? label : host;
    }

    /// <summary>
    /// The address to hand a pharmacy's staff, built from whichever way it is named.
    ///
    /// A stored value containing a dot is an address the pharmacy owns, so it is used as-is.
    /// A bare name is a prefix, so the platform's base domain is appended. Worth surfacing on
    /// the tenant page: it is the one thing a platform operator needs to pass on, and working
    /// it out by hand is exactly where a wrong link gets sent to a pharmacy's whole staff.
    /// </summary>
    public string SignInUrl(string domainName, bool secure, int? port = null)
    {
        var host = domainName.Trim().Trim('.').ToLowerInvariant();
        var baseDomain = _options.BaseDomain?.Trim().Trim('.').ToLowerInvariant();

        // A dot means it is already a whole address; anything else is a prefix under ours.
        if (!host.Contains('.') && !string.IsNullOrEmpty(baseDomain))
        {
            host = host + "." + baseDomain;
        }

        // The port is carried over so the link works in development, where the app is not on
        // 80 or 443. It drops out on the standard ports, which is where it will run for real.
        var authority = port is null or 80 or 443 ? host : $"{host}:{port}";

        return $"{(secure ? "https" : "http")}://{authority}/login";
    }
}
