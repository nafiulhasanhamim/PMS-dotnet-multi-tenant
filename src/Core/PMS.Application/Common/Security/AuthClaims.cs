namespace PMS.Application.Common.Security;

/// <summary>
/// The claim names this system issues and reads. One place, so a token written by the login
/// handler and a token read by the middleware can never drift apart.
/// </summary>
public static class AuthClaims
{
    /// <summary>The user's id. Standard JWT subject claim.</summary>
    public const string Subject = "sub";

    /// <summary>The pharmacy this session is for. Absent on a platform-admin token.</summary>
    public const string TenantId = "tenant_id";

    /// <summary>The role held *at that pharmacy*. Absent on a platform-admin token.</summary>
    public const string Role = "role";

    /// <summary>Present and true only on a platform-admin token.</summary>
    public const string PlatformAdmin = "platform_admin";
}
