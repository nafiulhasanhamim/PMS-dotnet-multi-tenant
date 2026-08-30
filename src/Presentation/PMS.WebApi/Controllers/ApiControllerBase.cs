using PMS.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace PMS.WebApi.Controllers;

/// <summary>
/// Base controller for API controllers with common functionality.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;

    /// <summary>
    /// Gets the mediator instance.
    /// </summary>
    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    /// <summary>
    /// Converts a Result to an appropriate ActionResult.
    /// </summary>
    protected IActionResult HandleResult(Result result)
    {
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return HandleError(result.Error);
    }

    /// <summary>
    /// Converts a Result with value to an appropriate ActionResult.
    /// </summary>
    protected IActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return HandleError(result.Error);
    }

    /// <summary>
    /// Converts a Result with value to a Created response.
    /// </summary>
    protected IActionResult HandleCreatedResult<T>(Result<T> result, string actionName, Func<T, object> routeValuesFactory)
    {
        if (result.IsSuccess)
        {
            return CreatedAtAction(actionName, routeValuesFactory(result.Value), result.Value);
        }

        return HandleError(result.Error);
    }

    private IActionResult HandleError(Error error)
    {
        return error.Code switch
        {
            var code when code.EndsWith(".NotFound") => NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = error.Description,
                Status = StatusCodes.Status404NotFound,
                Extensions = { ["code"] = error.Code }
            }),
            var code when code.StartsWith("Validation.") => BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Detail = error.Description,
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["code"] = error.Code }
            }),
            "Error.Conflict" => Conflict(new ProblemDetails
            {
                Title = "Conflict",
                Detail = error.Description,
                Status = StatusCodes.Status409Conflict,
                Extensions = { ["code"] = error.Code }
            }),
            "Error.Unauthorized" => Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = error.Description,
                Status = StatusCodes.Status401Unauthorized,
                Extensions = { ["code"] = error.Code }
            }),
            "Error.Forbidden" => StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Forbidden",
                Detail = error.Description,
                Status = StatusCodes.Status403Forbidden,
                Extensions = { ["code"] = error.Code }
            }),
            _ => BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = error.Description,
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["code"] = error.Code }
            })
        };
    }
}
