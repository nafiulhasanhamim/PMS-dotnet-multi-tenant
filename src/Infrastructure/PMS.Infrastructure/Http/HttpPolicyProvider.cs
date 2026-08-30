using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace PMS.Infrastructure.Http;

/// <summary>
/// Provides Polly resilience policies for HTTP clients.
/// Includes retry, circuit breaker, and timeout policies.
/// </summary>
public static class HttpPolicyProvider
{
    /// <summary>
    /// Creates a combined resilience policy with retry, circuit breaker, and timeout.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy(
        HttpClientOptions options,
        ILogger logger)
    {
        var policies = new List<IAsyncPolicy<HttpResponseMessage>>();

        // Timeout policy (innermost - applied first)
        policies.Add(GetTimeoutPolicy(options, logger));

        // Retry policy (applied after timeout)
        if (options.EnableRetry)
        {
            policies.Add(GetRetryPolicy(options, logger));
        }

        // Circuit breaker (outermost - applied last)
        if (options.EnableCircuitBreaker)
        {
            policies.Add(GetCircuitBreakerPolicy(options, logger));
        }

        // Policy.WrapAsync requires at least 2 policies
        if (policies.Count == 1)
        {
            return policies[0];
        }

        // Wrap policies: CircuitBreaker -> Retry -> Timeout
        return Policy.WrapAsync(policies.ToArray());
    }

    /// <summary>
    /// Creates a retry policy with exponential backoff.
    /// Only retries on transient failures (5xx, 408, network errors).
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(
        HttpClientOptions options,
        ILogger logger)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError() // 5xx, 408, HttpRequestException
            .Or<TimeoutRejectedException>() // Polly timeout
            .WaitAndRetryAsync(
                retryCount: options.RetryCount,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1) * options.RetryDelaySeconds),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    var clientName = context.TryGetValue("ClientName", out var name) ? name?.ToString() : options.Name;
                    var requestUri = context.TryGetValue("RequestUri", out var uri) ? uri?.ToString() : "Unknown";

                    if (outcome.Exception != null)
                    {
                        logger.LogWarning(
                            "HTTP request failed with exception. Client: {ClientName}, URL: {RequestUri}, " +
                            "Retry attempt: {RetryAttempt}/{MaxRetries}, Waiting: {WaitTime}s. Error: {ErrorMessage}",
                            clientName, requestUri, retryAttempt, options.RetryCount,
                            timespan.TotalSeconds, outcome.Exception.Message);
                    }
                    else
                    {
                        logger.LogWarning(
                            "HTTP request failed with status {StatusCode}. Client: {ClientName}, URL: {RequestUri}, " +
                            "Retry attempt: {RetryAttempt}/{MaxRetries}, Waiting: {WaitTime}s",
                            (int?)outcome.Result?.StatusCode, clientName, requestUri,
                            retryAttempt, options.RetryCount, timespan.TotalSeconds);
                    }
                });
    }

    /// <summary>
    /// Creates a circuit breaker policy.
    /// Opens after consecutive failures, preventing calls to failing services.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(
        HttpClientOptions options,
        ILogger logger)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: options.CircuitBreakerThreshold,
                durationOfBreak: TimeSpan.FromSeconds(options.CircuitBreakerDurationSeconds),
                onBreak: (outcome, breakDuration) =>
                {
                    logger.LogError(
                        "Circuit breaker OPENED for client '{ClientName}'. " +
                        "Too many failures detected. Circuit will remain open for {BreakDuration}s. " +
                        "Last error: {ErrorMessage}",
                        options.Name, breakDuration.TotalSeconds,
                        outcome.Exception?.Message ?? $"HTTP {(int?)outcome.Result?.StatusCode}");
                },
                onReset: () =>
                {
                    logger.LogInformation(
                        "Circuit breaker CLOSED for client '{ClientName}'. Service recovered.",
                        options.Name);
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation(
                        "Circuit breaker HALF-OPEN for client '{ClientName}'. Testing if service recovered.",
                        options.Name);
                });
    }

    /// <summary>
    /// Creates a timeout policy.
    /// Cancels requests that exceed the configured timeout.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(
        HttpClientOptions options,
        ILogger logger)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(
            timeout: TimeSpan.FromSeconds(options.TimeoutSeconds),
            timeoutStrategy: TimeoutStrategy.Optimistic,
            onTimeoutAsync: (context, timeout, task) =>
            {
                var clientName = context.TryGetValue("ClientName", out var name) ? name?.ToString() : options.Name;
                var requestUri = context.TryGetValue("RequestUri", out var uri) ? uri?.ToString() : "Unknown";

                logger.LogWarning(
                    "HTTP request TIMED OUT after {Timeout}s. Client: {ClientName}, URL: {RequestUri}",
                    timeout.TotalSeconds, clientName, requestUri);

                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// Creates a no-op policy (passthrough) when resilience is disabled.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetNoOpPolicy()
    {
        return Policy.NoOpAsync<HttpResponseMessage>();
    }
}
