using PMS.Application.Common.DTOs;
using PMS.Application.Features.Auth.Commands.PlatformLogin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PMS.WebApi.Controllers;

/// <summary>Sign-in for platform operators. No pharmacy involved.</summary>
[Route("api/platform/auth")]
public class PlatformAuthController : ApiControllerBase
{
    /// <summary>Signs in a platform operator.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] PlatformLoginCommand command, CancellationToken cancellationToken = default)
        => HandleResult(await Mediator.Send(command, cancellationToken));
}
