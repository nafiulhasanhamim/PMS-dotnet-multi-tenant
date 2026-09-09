using Microsoft.Extensions.Options;

namespace PMS.Web.Time;

/// <summary>
/// Configured display zone. <c>Display:TimeZone</c> takes a Windows id ("Bangladesh Standard
/// Time") or an IANA one ("Asia/Dhaka"); both resolve on .NET 8. Left unset, the server's own
/// zone is used.
/// </summary>
public sealed class DisplayOptions
{
    public const string SectionName = "Display";

    public string? TimeZone { get; set; }
}

/// <summary>
/// Turns the UTC instants the API returns into the wall-clock time the person reading the
/// screen is actually living in.
///
/// <para>This is the only place in the solution that knows a time zone exists. Everything
/// below it stores and transports UTC — see <c>UtcDateTimeConverter</c> — and the point of
/// that discipline is that this conversion has exactly one home, which can be corrected in
/// one edit when it is wrong.</para>
///
/// <para><b>Known limit.</b> One zone for the whole deployment. A platform hosting pharmacies
/// in different zones needs this per tenant, resolved from the tenant rather than from
/// configuration; the shape here does not fight that change, but it does not implement it.
/// Every pharmacy is in Bangladesh today.</para>
/// </summary>
public sealed class DisplayTimeZone
{
    private readonly TimeZoneInfo _zone;
    private readonly ILogger<DisplayTimeZone> _logger;

    public DisplayTimeZone(IOptions<DisplayOptions> options, ILogger<DisplayTimeZone> logger)
    {
        _logger = logger;
        _zone = Resolve(options.Value.TimeZone);
    }

    /// <summary>The zone times are displayed in. Exposed so a page can label a column.</summary>
    public TimeZoneInfo Zone => _zone;

    /// <summary>
    /// Converts a UTC instant to the display zone.
    ///
    /// <para>A value read back from SQL Server used to arrive as
    /// <see cref="DateTimeKind.Unspecified"/>, and
    /// <see cref="TimeZoneInfo.ConvertTimeFromUtc"/> throws on a
    /// <see cref="DateTimeKind.Local"/> input, so the kind is normalised before converting
    /// rather than trusted. Unspecified is taken to mean UTC, which is true of every
    /// timestamp this application stores.</para>
    /// </summary>
    public DateTime ToLocal(DateTime utc)
    {
        var asUtc = utc.Kind switch
        {
            DateTimeKind.Utc => utc,
            DateTimeKind.Local => utc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utc, DateTimeKind.Utc),
        };

        return TimeZoneInfo.ConvertTimeFromUtc(asUtc, _zone);
    }

    /// <summary>Formats a UTC instant in the display zone. Null renders as an em dash.</summary>
    public string Format(DateTime? utc, string format = "d MMM yyyy, HH:mm")
        => utc is null ? "—" : ToLocal(utc.Value).ToString(format);

    /// <summary>Formats the date only, in the display zone.</summary>
    public string FormatDate(DateTime? utc, string format = "d MMM yyyy")
        => Format(utc, format);

    private TimeZoneInfo Resolve(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return TimeZoneInfo.Local;
        }

        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var found))
        {
            return found;
        }

        // Falling back rather than throwing: a mistyped zone id should not take the site
        // down, and the wrong-but-close server zone is a better failure than a blank page.
        // It is a Warning and not a Debug because every timestamp on every screen is now
        // quietly in the wrong zone, which nobody notices by looking.
        _logger.LogWarning(
            "Display:TimeZone '{ConfiguredZone}' is not a zone this system recognises; "
            + "falling back to the server zone {FallbackZone}. Times on screen will be wrong "
            + "if those two differ",
            id, TimeZoneInfo.Local.Id);

        return TimeZoneInfo.Local;
    }
}
