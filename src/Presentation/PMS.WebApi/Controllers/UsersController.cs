using PMS.Application.Common.DTOs;
using PMS.Application.Features.Users.Commands.CreateTenantUser;
using PMS.Application.Features.Users.Queries.GetMyProfile;
using PMS.Application.Features.Users.Queries.GetTenantUsers;
using PMS.Application.Features.Users.Commands.SetMembershipActive;
using PMS.Domain.Enums;
using PMS.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PMS.WebApi.Controllers;

/// <summary>
/// Staff management within one pharmacy. The pharmacy always comes from the caller's token,
/// never from the request, so an Admin cannot reach into another one.
/// </summary>
[Route("api/users")]
public class UsersController : ApiControllerBase
{
    /// <summary>The current pharmacy's staff.</summary>
    [HttpGet]
    [Authorize(Policy = AuthenticationExtensions.TenantAdminPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<TenantUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken = default)
        => Ok(await Mediator.Send(new GetTenantUsersQuery(), cancellationToken));

    /// <summary>
    /// Adds a Pharmacist or Employee. An existing email is linked to this pharmacy instead of
    /// being recreated, and its password is left untouched.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthenticationExtensions.TenantAdminPolicy)]
    [ProducesResponseType(typeof(ProvisionedUserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProvisionedUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateTenantUserRequest request, CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(
            new CreateTenantUserCommand(request.Email, request.FullName, request.Password, request.Role),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return HandleResult(result);
        }

        return result.Value.Outcome == ProvisioningOutcome.UserCreated
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : Ok(result.Value);
    }

    /// <summary>Revokes access to this pharmacy. Other pharmacies are unaffected.</summary>
    [HttpPatch("{membershipId:guid}/deactivate")]
    [Authorize(Policy = AuthenticationExtensions.TenantAdminPolicy)]
    [ProducesResponseType(typeof(TenantUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        Guid membershipId, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new SetMembershipActiveCommand(membershipId, false), cancellationToken));

    /// <summary>Restores access to this pharmacy.</summary>
    [HttpPatch("{membershipId:guid}/reactivate")]
    [Authorize(Policy = AuthenticationExtensions.TenantAdminPolicy)]
    [ProducesResponseType(typeof(TenantUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reactivate(
        Guid membershipId, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new SetMembershipActiveCommand(membershipId, true), cancellationToken));

    /// <summary>
    /// The signed-in user, and their role at *this* pharmacy. The same call with a token from
    /// another pharmacy returns a different role for the same person.
    /// </summary>
    [HttpGet("me")]
    [Authorize(Policy = AuthenticationExtensions.TenantUserPolicy)]
    [ProducesResponseType(typeof(MyProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(new GetMyProfileQuery(), cancellationToken));
}

public sealed record CreateTenantUserRequest(
    string Email, string FullName, string Password, UserRole Role);
