using PMS.Application.Common.DTOs;
using PMS.Application.Features.Auth.Commands.TenantLogin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PMS.WebApi.Controllers;

/// <summary>Sign-in at a pharmacy.</summary>
[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    /// <summary>
    /// Signs in at the pharmacy identified by DomainName. A user with access to several
    /// simply logs in at each one's domain — there is no tenant picker, by design.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] TenantLoginCommand command, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(command, cancellationToken));
}
