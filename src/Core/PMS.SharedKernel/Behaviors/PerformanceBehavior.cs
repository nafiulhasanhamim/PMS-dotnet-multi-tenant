using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.SharedKernel.Behaviors;

/// <summary>
/// MediatR pipeline behavior that monitors request performance.
/// Logs a warning if request execution exceeds the configured threshold.
/// Includes full request details for debugging slow operations.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private readonly Stopwatch _timer = new();

    /// <summary>
    /// The threshold in milliseconds after which a warning is logged.
    /// </summary>
    private const int WarningThresholdMs = 500;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _timer.Start();

        var response = await next();

        _timer.Stop();

        var elapsedMilliseconds = _timer.ElapsedMilliseconds;

        if (elapsedMilliseconds > WarningThresholdMs)
        {
            var requestName = typeof(TRequest).Name;

            // Log with @ symbol to destructure the request object for debugging
            _logger.LogWarning(
                "Long running request: {RequestName} ({ElapsedMs}ms) - Request: {@Request}",
                requestName,
                elapsedMilliseconds,
                request);
        }

        return response;
    }
}
