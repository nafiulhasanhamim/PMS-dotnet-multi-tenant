using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Common.Provisioning;

/// <summary>
/// Grants someone access to a pharmacy, creating their identity only if they do not have one.
///
/// Shared by the platform endpoint that creates a pharmacy's first Admin and the tenant
/// endpoint that creates its staff, because the rule is identical and getting it wrong in one
/// place would be worse than not sharing it: overwriting an existing person's password
/// because an administrator elsewhere typed one.
/// </summary>
public sealed class UserProvisioner
{
    private readonly IIdentityQueries _identity;
    private readonly IRepository<User, IApplicationDbContext> _users;
    private readonly IRepository<UserTenantMembership, IApplicationDbContext> _memberships;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly IPasswordHasher _hasher;
    private readonly ILogger<UserProvisioner> _logger;

    public UserProvisioner(
        IIdentityQueries identity,
        IRepository<User, IApplicationDbContext> users,
        IRepository<UserTenantMembership, IApplicationDbContext> memberships,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        IPasswordHasher hasher,
        ILogger<UserProvisioner> logger)
    {
        _identity = identity;
        _users = users;
        _memberships = memberships;
        _unitOfWork = unitOfWork;
        _hasher = hasher;
        _logger = logger;
    }

    /// <summary>
    /// Attaches <paramref name="email"/> to <paramref name="tenantId"/> with the given role.
    ///
    /// If no account exists, one is created with the supplied password. If one does, **the
    /// password is ignored** and only the membership is added — an administrator at one
    /// pharmacy must never be able to change the credentials of someone who also works
    /// elsewhere. The outcome says which happened.
    /// </summary>
    public async Task<Result<ProvisionedUserDto>> ProvisionAsync(
        Guid tenantId, string email, string fullName, string password, UserRole role,
        CancellationToken cancellationToken = default)
    {
        if (role == UserRole.PlatformAdmin)
        {
            // An attempt to mint platform-wide power through a per-pharmacy endpoint. Almost
            // certainly a caller passing the wrong enum, but it is the shape a privilege
            // escalation would take, so it is recorded rather than merely refused.
            _logger.LogWarning(
                "Provisioning refused for {Email} at tenant {TenantId}: PlatformAdmin cannot "
                + "be granted at a pharmacy",
                email, tenantId);

            return Result.Failure<ProvisionedUserDto>(Error.Validation(
                nameof(role),
                "PlatformAdmin cannot be granted at a pharmacy. It is a platform-level role."));
        }

        var existing = await _identity.FindUserByEmailAsync(email, cancellationToken);
        var outcome = existing is null
            ? ProvisioningOutcome.UserCreated
            : ProvisioningOutcome.ExistingUserLinked;

        var user = existing;
        if (user is null)
        {
            user = new User(email, fullName, _hasher.Hash(password));
            await _users.AddAsync(user, cancellationToken);
        }
        else if (!user.IsGloballyActive)
        {
            // Their identity is disabled platform-wide, so a new membership would be dead on
            // arrival. Say so rather than creating something that cannot be used.
            _logger.LogWarning(
                "Provisioning refused for {Email} at tenant {TenantId}: the account is "
                + "disabled platform-wide (user {UserId})",
                email, tenantId, user.Id);

            return Result.Failure<ProvisionedUserDto>(Error.Conflict(
                "That account is disabled platform-wide and cannot be given new access."));
        }

        var already = await _identity.FindMembershipAsync(tenantId, user.Id, cancellationToken);
        if (already is not null)
        {
            // Records the role they already hold, which is the question actually being asked
            // underneath: someone re-adding a colleague usually means to change their role,
            // and this line says what it currently is.
            _logger.LogWarning(
                "Provisioning refused for {Email} at tenant {TenantId}: already a member "
                + "as {ExistingRole} (active={IsActive})",
                email, tenantId, already.Role, already.IsActive);

            return Result.Failure<ProvisionedUserDto>(Error.Conflict(
                "That person already has access to this pharmacy."));
        }

        var membership = UserTenantMembership.ForTenant(tenantId, user.Id, role);
        await _memberships.AddAsync(membership, cancellationToken);

        // One transaction: an identity without its membership would be an account nobody can
        // use, and a membership without its identity cannot exist.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Granting someone access to a pharmacy is the event an audit asks about first, so it
        // is Information and it names the role. The outcome is on the line because the two
        // cases differ in a way that matters later: a linked account keeps the password it
        // already had, so whoever typed one here will find it was ignored, exactly as
        // intended and never obviously.
        _logger.LogInformation(
            "Access granted to {Email} at tenant {TenantId} as {Role} ({Outcome}, "
            + "user {UserId}, membership {MembershipId})",
            email, tenantId, role, outcome, user.Id, membership.Id);

        return new ProvisionedUserDto(user.Id, membership.Id, user.Email, role, outcome);
    }
}
