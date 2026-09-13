using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PMS.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace PMS.IntegrationTests.Api;

/// <summary>
/// A 400 has to say which field was wrong.
///
/// The exception middleware builds a ValidationProblemDetails but holds it in a variable typed
/// as ProblemDetails, and System.Text.Json serializes the declared type — so the `errors`
/// dictionary was dropped from every response, leaving clients a 400 whose only content was
/// "one or more validation errors occurred". The UI could then show nothing useful at all.
/// </summary>
public class ValidationProblemTests : IntegrationTestBase
{
    public ValidationProblemTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task EmptyLogin_ReturnsFieldLevelErrors()
    {
        // Login is anonymous, so this reaches validation without needing a token.
        var response = await Client.PostAsJsonAsync(
            "/api/auth/login", new { domainName = "", email = "", password = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        body.RootElement.TryGetProperty("errors", out var errors)
            .Should().BeTrue("a validation problem must carry the per-field messages");

        errors.EnumerateObject().Select(e => e.Name)
            .Should().Contain(new[] { "DomainName", "Email", "Password" });
    }

    [Fact]
    public async Task ValidationProblem_KeepsTheRfc7807Shape()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/auth/login", new { domainName = "", email = "", password = "" });

        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        body.RootElement.GetProperty("status").GetInt32().Should().Be(400);
        body.RootElement.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
        body.RootElement.GetProperty("instance").GetString().Should().Be("/api/auth/login");
    }
}
