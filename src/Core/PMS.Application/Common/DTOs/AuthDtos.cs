using PMS.Domain.Enums;

namespace PMS.Application.Common.DTOs;

/// <summary>What a successful login returns to a client.</summary>
public sealed record AuthResultDto(
    string Token,
    DateTime ExpiresAtUtc,
    Guid UserId,
    string Email,
    string FullName,
    /// <summary>Null for a platform admin — they are not inside any pharmacy.</summary>
    TenantSummaryDto? Tenant,
    /// <summary>The role at that pharmacy, or PlatformAdmin.</summary>
    UserRole Role);

public sealed record TenantSummaryDto(Guid Id, string Name, string DomainName);

public sealed record TenantDto(
    Guid Id,
    string Name,
    string DomainName,
    TenantStatus Status,
    string? SubscriptionPlan,
    DateTime CreatedOnUtc);

/// <summary>One row of a pharmacy's staff list: the membership joined to its user.</summary>
public sealed record TenantUserDto(
    Guid MembershipId,
    Guid UserId,
    string Email,
    string FullName,
    UserRole Role,
    bool IsActive,
    DateTime JoinedAt);

/// <summary>
/// Whether provisioning created a new account or attached an existing one. The caller needs
/// to know, because on the second path the password they supplied was ignored.
/// </summary>
public enum ProvisioningOutcome
{
    UserCreated = 0,
    ExistingUserLinked = 1,
}

public sealed record ProvisionedUserDto(
    Guid UserId,
    Guid MembershipId,
    string Email,
    UserRole Role,
    ProvisioningOutcome Outcome);

public sealed record MyProfileDto(
    Guid UserId,
    string Email,
    string FullName,
    UserRole Role,
    TenantSummaryDto Tenant);
