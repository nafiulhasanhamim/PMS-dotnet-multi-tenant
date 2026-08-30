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
