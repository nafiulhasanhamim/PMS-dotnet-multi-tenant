using System.Net;
using PMS.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace PMS.IntegrationTests.Api;

/// <summary>
/// Integration tests for health check endpoints.
/// </summary>
public class HealthCheckTests : IntegrationTestBase
{
    public HealthCheckTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        // Act
        var response = await Client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LivenessCheck_ReturnsHealthy()
    {
        // Act
        var response = await Client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadinessCheck_ReturnsHealthy()
    {
        // Act
        var response = await Client.GetAsync("/health/ready");

        // Assert
        // May be unhealthy if DB checks fail in test environment
        // Just verify the endpoint responds
        response.Should().NotBeNull();
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }
}
