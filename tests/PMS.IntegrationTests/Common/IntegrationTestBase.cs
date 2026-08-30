using System.Net.Http.Json;
using PMS.Persistence.Contexts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PMS.IntegrationTests.Common;

/// <summary>
/// Base class for integration tests providing common setup and utilities.
/// Implements IAsyncLifetime for async setup/teardown.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    protected readonly CustomWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    /// <summary>
    /// Called before each test - resets the database.
    /// </summary>
    public virtual async Task InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
    }

    /// <summary>
    /// Called after each test - cleanup.
    /// </summary>
    public virtual Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a fresh DbContext for seeding test data.
    /// </summary>
    protected ApplicationDbContext GetDbContext()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    /// <summary>
    /// Posts JSON data and returns the response.
    /// </summary>
    protected async Task<HttpResponseMessage> PostAsJsonAsync<T>(string url, T data)
    {
        return await Client.PostAsJsonAsync(url, data);
    }

    /// <summary>
    /// Puts JSON data and returns the response.
    /// </summary>
    protected async Task<HttpResponseMessage> PutAsJsonAsync<T>(string url, T data)
    {
        return await Client.PutAsJsonAsync(url, data);
    }

    /// <summary>
    /// Gets and deserializes the response.
    /// </summary>
    protected async Task<T?> GetAsync<T>(string url)
    {
        var response = await Client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }
}
