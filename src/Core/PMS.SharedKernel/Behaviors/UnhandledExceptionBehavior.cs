using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.SharedKernel.Behaviors;

/// <summary>
/// MediatR pipeline behavior that catches and logs unhandled exceptions.
/// Ensures all exceptions are properly logged with full request details before being re-thrown.
/// This should be the outermost behavior in the pipeline.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class UnhandledExceptionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> _logger;

    public UnhandledExceptionBehavior(ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            var requestName = typeof(TRequest).Name;

            // Log with @ symbol to destructure the request object for structured logging
            _logger.LogError(
                ex,
                "Unhandled exception for request {RequestName} - Request: {@Request}",
                requestName,
                request);

            throw;
        }
    }
}
