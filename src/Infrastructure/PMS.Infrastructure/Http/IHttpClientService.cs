namespace PMS.Infrastructure.Http;

/// <summary>
/// Abstraction for making HTTP requests with built-in resilience.
/// </summary>
/// <remarks>
/// Registered via IHttpClientFactory using AddResilientHttpClient() extension method.
/// Do not use service lifetime markers - registration is handled explicitly.
/// </remarks>
public interface IHttpClientService
{
    /// <summary>
    /// Sends a GET request to the specified URI.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response to.</typeparam>
    /// <param name="uri">The request URI.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized response.</returns>
    Task<T?> GetAsync<T>(string uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a POST request with JSON content.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request body.</typeparam>
    /// <typeparam name="TResponse">The type to deserialize the response to.</typeparam>
    /// <param name="uri">The request URI.</param>
    /// <param name="content">The request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized response.</returns>
    Task<TResponse?> PostAsync<TRequest, TResponse>(string uri, TRequest content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a PUT request with JSON content.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request body.</typeparam>
    /// <typeparam name="TResponse">The type to deserialize the response to.</typeparam>
    /// <param name="uri">The request URI.</param>
    /// <param name="content">The request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized response.</returns>
    Task<TResponse?> PutAsync<TRequest, TResponse>(string uri, TRequest content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a DELETE request.
    /// </summary>
    /// <param name="uri">The request URI.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successful (2xx status code).</returns>
    Task<bool> DeleteAsync(string uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a PATCH request with JSON content.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request body.</typeparam>
    /// <typeparam name="TResponse">The type to deserialize the response to.</typeparam>
    /// <param name="uri">The request URI.</param>
    /// <param name="content">The request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized response.</returns>
    Task<TResponse?> PatchAsync<TRequest, TResponse>(string uri, TRequest content, CancellationToken cancellationToken = default);
}
