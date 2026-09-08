using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Users.Queries.GetMyProfile;

public sealed class GetMyProfileQueryHandler
    : IRequestHandler<GetMyProfileQuery, Result<MyProfileDto>>
{
    private readonly ICurrentUserService _user;
    private readonly ICurrentTenantService _tenant;
    private readonly IIdentityQueries _identity;

    public GetMyProfileQueryHandler(
        ICurrentUserService user, ICurrentTenantService tenant, IIdentityQueries identity)
    {
        _user = user;
        _tenant = tenant;
        _identity = identity;
    }

    public async Task<Result<MyProfileDto>> Handle(
        GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var userId = _user.UserGuid;
        if (userId is null)
        {
            return Result.Failure<MyProfileDto>(Error.Unauthorized("Not signed in."));
        }

        var user = await _identity.FindUserByIdAsync(userId.Value, cancellationToken);
        var tenant = await _identity.FindTenantByIdAsync(_tenant.TenantId, cancellationToken);

        // The role is read from the membership rather than the token, so revoking access
        // takes effect on the next call instead of when the token expires.
        var membership = await _identity.FindMembershipAsync(
            _tenant.TenantId, userId.Value, cancellationToken);

        if (user is null || tenant is null || membership is null || !membership.IsActive)
        {
            return Result.Failure<MyProfileDto>(
                Error.Unauthorized("Your access to this pharmacy is no longer valid."));
        }

        return new MyProfileDto(
            user.Id, user.Email, user.FullName, membership.Role,
            new TenantSummaryDto(tenant.Id, tenant.Name, tenant.DomainName));
    }
}
