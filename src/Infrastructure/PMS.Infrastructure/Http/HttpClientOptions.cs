namespace PMS.Infrastructure.Http;

/// <summary>
/// Configuration options for HTTP clients with resilience policies.
/// </summary>
public class HttpClientOptions
{
    /// <summary>
    /// Gets or sets the name of the HTTP client.
    /// </summary>
    public string Name { get; set; } = "Default";

    /// <summary>
    /// Gets or sets the base URL for the HTTP client.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the timeout in seconds for HTTP requests.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether retry policy is enabled.
    /// </summary>
    public bool EnableRetry { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base delay in seconds between retries (exponential backoff).
    /// </summary>
    public double RetryDelaySeconds { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether circuit breaker policy is enabled.
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of failures before the circuit breaker opens.
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the duration in seconds the circuit breaker stays open.
    /// </summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets optional default headers to include in all requests.
    /// </summary>
    public Dictionary<string, string> DefaultHeaders { get; set; } = [];
}
