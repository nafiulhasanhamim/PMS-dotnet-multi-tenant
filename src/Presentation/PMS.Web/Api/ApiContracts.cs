namespace PMS.Web.Api;

// The API's wire contract, declared locally on purpose.
//
// This project references nothing from the solution: it talks to PMS.WebApi over HTTP and
// nothing else, exactly as a mobile or desktop client would. Sharing the server's DTO
// assembly would be less typing, but it would also mean this app could quietly start calling
// handlers directly — and then the API would no longer be proven by anything.
//
// The cost is these few records. They must match the server's JSON; the enum values must
// match the server's numeric values.

public enum UserRole
{
    PlatformAdmin = 0,
    Admin = 1,
    Pharmacist = 2,
    Employee = 3,
}

public enum TenantStatus
{
    Trial = 0,
    Active = 1,
    Suspended = 2,
}

public enum ProvisioningOutcome
{
    UserCreated = 0,
    ExistingUserLinked = 1,
}

public sealed record TenantSummary(Guid Id, string Name, string DomainName);

public sealed record AuthResult(
    string Token,
    DateTime ExpiresAtUtc,
    Guid UserId,
    string Email,
    string FullName,
    TenantSummary? Tenant,
    UserRole Role);

public sealed record TenantModel(
    Guid Id,
    string Name,
    string DomainName,
    TenantStatus Status,
    string? SubscriptionPlan,
    DateTime CreatedOnUtc);

public sealed record TenantUserModel(
    Guid MembershipId,
    Guid UserId,
    string Email,
    string FullName,
    UserRole Role,
    bool IsActive,
    DateTime JoinedAt);

public sealed record ProvisionedUser(
    Guid UserId,
    Guid MembershipId,
    string Email,
    UserRole Role,
    ProvisioningOutcome Outcome);

public sealed record MyProfile(
    Guid UserId,
    string Email,
    string FullName,
    UserRole Role,
    TenantSummary Tenant);

// --- requests ---

public sealed record PlatformLoginRequest(string Email, string Password);

public sealed record TenantLoginRequest(string DomainName, string Email, string Password);

public sealed record CreateTenantRequest(string Name, string DomainName, string? SubscriptionPlan);

public sealed record CreateTenantAdminRequest(string Email, string FullName, string Password);

public sealed record UpdateTenantStatusRequest(TenantStatus Status);

public sealed record CreateTenantUserRequest(
    string Email, string FullName, string Password, UserRole Role);

/// <summary>
/// The API's RFC 7807 error shape. `code` is the machine-readable discriminator — the UI
/// switches on that, never on the human-facing detail text.
/// </summary>
/// <summary>
/// What kind of failure came back, so pages handle it consistently instead of each one
/// re-deriving it from a status code.
/// </summary>
public enum ApiFailure
{
    /// <summary>The request itself was rejected — show it on the form.</summary>
    Rejected = 0,

    /// <summary>The token is gone or expired. End the local session and say so.</summary>
    SessionExpired = 1,

    /// <summary>Authenticated, but not allowed. Show the permission-denied page.</summary>
    Forbidden = 2,

    /// <summary>The pharmacy is suspended or gone. End the session — the cookie is a lie.</summary>
    TenantUnavailable = 3,

    /// <summary>The API broke, or could not be reached. Generic page, detail to the log.</summary>
    ServerError = 4,
}

public sealed record ApiProblem(
    string? Title,
    string? Detail,
    int? Status,
    string? Code,
    Dictionary<string, string[]>? Errors = null)
{
    /// <summary>
    /// What to show the person who is looking at the screen.
    ///
    /// For a validation failure the API's own detail is useless on its own — it says "one or
    /// more validation errors occurred" — so the field messages are what get shown instead.
    /// </summary>
    public string Message
    {
        get
        {
            if (Errors is { Count: > 0 })
            {
                return string.Join(" ", Errors.SelectMany(e => e.Value).Distinct());
            }

            return Detail ?? Title ?? "The request could not be completed.";
        }
    }

    /// <summary>
    /// Classifies the failure once, here, rather than in every page model.
    ///
    /// The 403 split is the part worth reading. A 403 can mean "your role is not enough",
    /// which leaves the session perfectly valid, or it can mean "this pharmacy is suspended",
    /// which makes the local cookie misleading and has to end the session. The API
    /// distinguishes them with a code, so this does too — treating both the same would either
    /// sign people out over a permissions slip or keep a suspended pharmacy's session alive.
    /// </summary>
    public ApiFailure Failure => Status switch
    {
        401 => ApiFailure.SessionExpired,
        403 when Code is "Tenant.Inactive" or "Tenant.NotFound" => ApiFailure.TenantUnavailable,
        403 => ApiFailure.Forbidden,
        >= 500 => ApiFailure.ServerError,
        null => ApiFailure.ServerError,
        _ => ApiFailure.Rejected,
    };

    /// <summary>Field-level messages keyed by property name, for binding onto ModelState.</summary>
    public IEnumerable<KeyValuePair<string, string[]>> FieldErrors =>
        Errors ?? Enumerable.Empty<KeyValuePair<string, string[]>>();
}

/// <summary>
/// An API call's outcome. Modelled rather than thrown: a rejected login or a duplicate
/// domain is an expected answer that belongs on the page, not an exception.
/// </summary>
public sealed record ApiResult<T>(bool IsSuccess, T? Value, ApiProblem? Problem)
{
    public static ApiResult<T> Ok(T value) => new(true, value, null);
    public static ApiResult<T> Fail(ApiProblem problem) => new(false, default, problem);

    public string ErrorMessage => Problem?.Message ?? "Something went wrong.";
}
