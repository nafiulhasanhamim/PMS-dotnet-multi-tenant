using PMS.Application.Common.DTOs;
using PMS.Application.Features.Tenants.Commands.CreateTenant;
using PMS.Application.Features.Tenants.Commands.CreateTenantAdmin;
using PMS.Application.Features.Tenants.Queries.GetTenant;
using PMS.Application.Features.Tenants.Queries.GetTenants;
using PMS.Application.Features.Tenants.Queries.GetTenantUsers;
using PMS.Application.Features.Tenants.Commands.UpdateTenantStatus;
using PMS.Domain.Enums;
using PMS.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PMS.WebApi.Controllers;

/// <summary>
/// Onboarding, for platform operators only. Guarded by the platform_admin claim, which a
/// tenant token never carries.
/// </summary>
[Route("api/platform/tenants")]
[Authorize(Policy = AuthenticationExtensions.PlatformAdminPolicy)]
public class PlatformTenantsController : ApiControllerBase
{
    /// <summary>Every pharmacy, whatever its status.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TenantDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenants(CancellationToken cancellationToken = default)
        => Ok(await Mediator.Send(new GetTenantsQuery(), cancellationToken));

    /// <summary>One pharmacy by id.</summary>
    [HttpGet("{tenantId:guid}")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenant(
        Guid tenantId, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(new GetTenantQuery(tenantId), cancellationToken));

    /// <summary>
    /// A named pharmacy's staff. Platform-only: a tenant token cannot reach this controller,
    /// and the tenant id in the route is the only thing that decides which pharmacy is read.
    /// </summary>
    [HttpGet("{tenantId:guid}/users")]
    [ProducesResponseType(typeof(IReadOnlyList<TenantUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenantUsers(
        Guid tenantId, CancellationToken cancellationToken = default)
        => Ok(await Mediator.Send(new GetTenantUsersQuery(tenantId), cancellationToken));

    /// <summary>Onboards a pharmacy. It starts on Trial.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTenant(
        [FromBody] CreateTenantCommand command, CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleCreatedResult(result, nameof(GetTenants), t => new { id = t.Id });
    }

    /// <summary>Suspends or restores a pharmacy.</summary>
    [HttpPatch("{tenantId:guid}/status")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        Guid tenantId, [FromBody] UpdateTenantStatusRequest request,
        CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(
            new UpdateTenantStatusCommand(tenantId, request.Status), cancellationToken));

    /// <summary>
    /// Creates a pharmacy's first Admin — the only route by which a new pharmacy gets a user.
    /// An email that already exists is linked rather than recreated, and its password is left
    /// alone; the response says which happened.
    /// </summary>
    [HttpPost("{tenantId:guid}/admin-user")]
    [ProducesResponseType(typeof(ProvisionedUserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProvisionedUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAdminUser(
        Guid tenantId, [FromBody] CreateTenantAdminRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(
            new CreateTenantAdminCommand(tenantId, request.Email, request.FullName, request.Password),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return HandleResult(result);
        }

        // 201 when an account was created, 200 when an existing one was linked — the client
        // needs to know whether the password it sent was used.
        return result.Value.Outcome == ProvisioningOutcome.UserCreated
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : Ok(result.Value);
    }
}

public sealed record UpdateTenantStatusRequest(TenantStatus Status);

public sealed record CreateTenantAdminRequest(string Email, string FullName, string Password);
