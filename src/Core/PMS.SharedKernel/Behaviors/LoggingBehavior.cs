using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.SharedKernel.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs request execution.
/// Logs request start, completion with timing, and generates a unique request ID for correlation.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var requestId = Guid.NewGuid().ToString("N")[..8]; // 8-char unique ID for correlation

        _logger.LogInformation(
            "[{RequestId}] Starting request {RequestName}",
            requestId,
            requestName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();

            stopwatch.Stop();
            _logger.LogInformation(
                "[{RequestId}] Completed request {RequestName} in {ElapsedMs}ms",
                requestId,
                requestName,
                stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "[{RequestId}] Request {RequestName} failed after {ElapsedMs}ms",
                requestId,
                requestName,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}
