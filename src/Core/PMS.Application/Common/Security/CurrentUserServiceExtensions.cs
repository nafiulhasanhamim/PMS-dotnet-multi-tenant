using PMS.Domain.Enums;
using PMS.SharedKernel.Interfaces;

namespace PMS.Application.Common.Security;

/// <summary>
/// Typed access to the current user's role.
///
/// The parsing lives here rather than on ICurrentUserService because that interface sits in
/// SharedKernel, which must not reference Domain — so it can only carry the role as a string.
/// </summary>
public static class CurrentUserServiceExtensions
{
    /// <summary>
    /// The role held at the current pharmacy, or null for a platform admin, an anonymous
    /// request, or a token carrying a role this build does not recognise.
    /// </summary>
    public static UserRole? TenantRole(this ICurrentUserService user) =>
        Enum.TryParse<UserRole>(user.TenantRoleName, ignoreCase: true, out var role)
            ? role
            : null;

    /// <summary>True when the current user holds <paramref name="role"/> at this pharmacy.</summary>
    public static bool IsInTenantRole(this ICurrentUserService user, UserRole role) =>
        user.TenantRole() == role;
}
