using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace PMS.WebApi.Middleware;

/// <summary>
/// Global exception handling middleware.
/// Catches unhandled exceptions and returns appropriate HTTP responses.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, problemDetails) = exception switch
        {
            ValidationException validationEx => HandleValidationException(validationEx),
            UnauthorizedAccessException => HandleUnauthorizedException(),
            ArgumentException argEx => HandleArgumentException(argEx),
            InvalidOperationException invalidOpEx => HandleInvalidOperationException(invalidOpEx),
            _ => HandleUnknownException(exception)
        };

        // Log the exception
        if (statusCode >= 500)
        {
            _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);
        }
        else
        {
            _logger.LogWarning(exception, "A handled exception occurred: {Message}", exception.Message);
        }

        // Include stack trace in development
        if (_environment.IsDevelopment() && statusCode >= 500)
        {
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
        }

        problemDetails.Instance = context.Request.Path;
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        // Serialize against the RUNTIME type, not the declared one. `problemDetails` is typed
        // as ProblemDetails, and System.Text.Json writes only the declared type's properties —
        // which silently dropped the `errors` dictionary off every ValidationProblemDetails,
        // leaving clients a 400 that said "one or more validation errors occurred" and nothing
        // about which field.
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problemDetails, problemDetails.GetType(), JsonOptions));
    }

    private static (int StatusCode, ProblemDetails ProblemDetails) HandleValidationException(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());

        var problemDetails = new ValidationProblemDetails(errors)
        {
            Title = "Validation Failed",
            Detail = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        };

        return (StatusCodes.Status400BadRequest, problemDetails);
    }

    private static (int StatusCode, ProblemDetails ProblemDetails) HandleUnauthorizedException()
    {
        var problemDetails = new ProblemDetails
        {
            Title = "Unauthorized",
            Detail = "You are not authorized to access this resource.",
            Status = StatusCodes.Status401Unauthorized,
            Type = "https://tools.ietf.org/html/rfc7235#section-3.1"
        };

        return (StatusCodes.Status401Unauthorized, problemDetails);
    }

    private static (int StatusCode, ProblemDetails ProblemDetails) HandleArgumentException(ArgumentException exception)
    {
        var problemDetails = new ProblemDetails
        {
            Title = "Bad Request",
            Detail = exception.Message,
            Status = StatusCodes.Status400BadRequest,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        };

        return (StatusCodes.Status400BadRequest, problemDetails);
    }

    private static (int StatusCode, ProblemDetails ProblemDetails) HandleInvalidOperationException(InvalidOperationException exception)
    {
        var problemDetails = new ProblemDetails
        {
            Title = "Invalid Operation",
            Detail = exception.Message,
            Status = StatusCodes.Status400BadRequest,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        };

        return (StatusCodes.Status400BadRequest, problemDetails);
    }

    private (int StatusCode, ProblemDetails ProblemDetails) HandleUnknownException(Exception exception)
    {
        var problemDetails = new ProblemDetails
        {
            Title = "Internal Server Error",
            Detail = _environment.IsDevelopment()
                ? exception.Message
                : "An unexpected error occurred. Please try again later.",
            Status = StatusCodes.Status500InternalServerError,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
        };

        return (StatusCodes.Status500InternalServerError, problemDetails);
    }
}

/// <summary>
/// Extension methods for registering the exception middleware.
/// </summary>
public static class ExceptionMiddlewareExtensions
{
    /// <summary>
    /// Adds the global exception handling middleware to the pipeline.
    /// </summary>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionMiddleware>();
    }
}
