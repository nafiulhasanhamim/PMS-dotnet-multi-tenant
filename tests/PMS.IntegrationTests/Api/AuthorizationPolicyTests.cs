using System.Net;
using System.Net.Http.Headers;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PMS.IntegrationTests.Api;

/// <summary>
/// Guards the wiring between the token the API issues and the policies it enforces.
///
/// This exists because that seam broke silently once. JwtBearer maps well-known short claim
/// names onto the old WS-* URIs by default, so a token issued with <c>role</c> arrived as
/// <c>.../claims/role</c>, <c>RequireClaim("role", "Admin")</c> matched nothing, and every
/// pharmacy-Admin endpoint answered 403 to a perfectly valid Admin token. Nothing in the unit,
/// schema or architecture suites could see it: the token was correct, the policy was correct,
/// and only the running pipeline disagreed.
/// </summary>
public class AuthorizationPolicyTests : IntegrationTestBase
{
    public AuthorizationPolicyTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    /// <summary>
    /// Creates a usable pharmacy and returns its id.
    ///
    /// Needed even for tests that only care about roles: the tenant-resolution middleware runs
    /// before the endpoint and answers 403 for a tenant it cannot find, so a token naming a
    /// made-up pharmacy would be refused for the wrong reason and prove nothing about policy.
    /// </summary>
    private async Task<Guid> CreateActiveTenantAsync(string domainName)
    {
        using var db = Factory.CreateDbContext();

        var tenant = new Tenant($"Test Pharmacy {domainName}", domainName, "Standard");
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        return tenant.Id;
    }

    private TokenResult IssueTenantToken(Guid tenantId, UserRole role)
    {
        using var scope = Factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        return tokens.IssueForTenantUser(Guid.NewGuid(), tenantId, role);
    }

    private TokenResult IssuePlatformToken()
    {
        using var scope = Factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        return tokens.IssueForPlatformAdmin(Guid.NewGuid());
    }

    private HttpRequestMessage Authorized(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    [Fact]
    public async Task TenantAdminToken_IsAcceptedByTenantAdminEndpoint()
    {
        var tenantId = await CreateActiveTenantAsync("admin-accepted.test");
        var token = IssueTenantToken(tenantId, UserRole.Admin);

        var response = await Client.SendAsync(Authorized(HttpMethod.Get, "/api/users", token.Token));

        // The list itself is empty — the point is that the request was authorized at all.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PharmacistToken_IsRefusedByTenantAdminEndpoint()
    {
        var tenantId = await CreateActiveTenantAsync("pharmacist-refused.test");
        var token = IssueTenantToken(tenantId, UserRole.Pharmacist);

        var response = await Client.SendAsync(Authorized(HttpMethod.Get, "/api/users", token.Token));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PlatformToken_CannotReachTenantEndpoints()
    {
        // A platform token carries no tenant, so it must not satisfy a tenant policy — the
        // filters would resolve to nothing and it would read as "this pharmacy is empty".
        var token = IssuePlatformToken();

        var response = await Client.SendAsync(Authorized(HttpMethod.Get, "/api/users", token.Token));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TenantToken_CannotReachPlatformEndpoints()
    {
        var tenantId = await CreateActiveTenantAsync("tenant-vs-platform.test");
        var token = IssueTenantToken(tenantId, UserRole.Admin);

        var response = await Client.SendAsync(
            Authorized(HttpMethod.Get, "/api/platform/tenants", token.Token));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TenantToken_CannotReadAnotherPharmacysStaff()
    {
        // The platform staff read takes a tenant id in the route, so it is the one endpoint
        // where a caller could name a pharmacy that is not theirs. It must be unreachable
        // with a tenant token — including one for the very pharmacy being asked about.
        var tenantId = await CreateActiveTenantAsync("cross-tenant-read.test");
        var token = IssueTenantToken(tenantId, UserRole.Admin);

        var response = await Client.SendAsync(Authorized(
            HttpMethod.Get, $"/api/platform/tenants/{tenantId}/users", token.Token));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PlatformToken_CanReadANamedPharmacysStaff()
    {
        var tenantId = await CreateActiveTenantAsync("platform-staff-read.test");
        var token = IssuePlatformToken();

        var response = await Client.SendAsync(Authorized(
            HttpMethod.Get, $"/api/platform/tenants/{tenantId}/users", token.Token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NoToken_IsUnauthorized()
    {
        var response = await Client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
