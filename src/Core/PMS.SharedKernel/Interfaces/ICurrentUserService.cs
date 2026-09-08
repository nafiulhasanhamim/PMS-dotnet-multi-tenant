using PMS.SharedKernel.DependencyInjection;

namespace PMS.SharedKernel.Interfaces;

/// <summary>
/// Provides access to the current authenticated user's information.
/// Registered as Scoped lifetime. Typically reads from HttpContext.User claims.
/// </summary>
public interface ICurrentUserService : IScopedService
{
    /// <summary>
    /// Gets the unique identifier of the current user.
    /// Returns null if the user is not authenticated.
    /// </summary>
    string? UserId { get; }

    /// <summary>The signed-in user's id, or null when there is no valid one.</summary>
    Guid? UserGuid { get; }

    /// <summary>
    /// The role held at the *current* pharmacy as written in the token, or null on a
    /// platform-admin or anonymous request. The same person can resolve to a different role
    /// at another pharmacy.
    ///
    /// A string rather than the UserRole enum because SharedKernel must not depend on Domain
    /// — the architecture tests enforce that. Use
    /// <c>ICurrentUserServiceExtensions.TenantRole()</c> in the Application layer for the
    /// typed value.
    /// </summary>
    string? TenantRoleName { get; }

    /// <summary>
    /// Gets the username of the current user.
    /// Returns null if the user is not authenticated.
    /// </summary>
    string? UserName { get; }

    /// <summary>
    /// Gets the email address of the current user.
    /// Returns null if the user is not authenticated.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Gets a value indicating whether the current user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the roles assigned to the current user.
    /// </summary>
    IEnumerable<string> Roles { get; }

    /// <summary>
    /// Checks if the current user has the specified role.
    /// </summary>
    /// <param name="role">The role to check.</param>
    /// <returns>True if the user has the role; otherwise, false.</returns>
    bool IsInRole(string role);
}
