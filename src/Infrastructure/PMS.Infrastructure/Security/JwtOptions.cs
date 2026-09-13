namespace PMS.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "PMS";
    public string Audience { get; set; } = "PMS";

    /// <summary>Signing key. Must be at least 32 bytes for HS256.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 480;
}
