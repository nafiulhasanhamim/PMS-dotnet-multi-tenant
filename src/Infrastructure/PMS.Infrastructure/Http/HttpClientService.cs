using System.Net.Http.Json;
using System.Text.Json;

namespace PMS.Infrastructure.Http;

/// <summary>
/// HTTP client service implementation with JSON serialization.
/// Uses IHttpClientFactory for proper HTTP client management.
/// </summary>
public class HttpClientService : IHttpClientService
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public HttpClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string uri, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string uri,
        TRequest content,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(uri, content, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TResponse?> PutAsync<TRequest, TResponse>(
        string uri,
        TRequest content,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(uri, content, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string uri, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync(uri, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <inheritdoc />
    public async Task<TResponse?> PatchAsync<TRequest, TResponse>(
        string uri,
        TRequest content,
        CancellationToken cancellationToken = default)
    {
        var jsonContent = JsonContent.Create(content, options: JsonOptions);
        var response = await _httpClient.PatchAsync(uri, jsonContent, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
    }
}
