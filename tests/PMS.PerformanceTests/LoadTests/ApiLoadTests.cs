using System.Text;
using System.Text.Json;
using PMS.PerformanceTests.Common;
using Microsoft.FSharp.Core;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;
using Xunit;
using Xunit.Abstractions;

namespace PMS.PerformanceTests.LoadTests;

/// <summary>
/// Load tests for API endpoints using NBomber.
/// Tests API throughput and response times under load.
/// </summary>
public class ApiLoadTests : IClassFixture<LoadTestWebApplicationFactory>
{
    private readonly LoadTestWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly HttpClient _client;

    public ApiLoadTests(LoadTestWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _output = output;
        _client = _factory.CreateClient();
    }

    private static bool IsSuccessResponse(Response<HttpResponseMessage> response)
    {
        return FSharpOption<HttpResponseMessage>.get_IsSome(response.Payload) &&
               response.Payload.Value.IsSuccessStatusCode;
    }

    [Fact(Skip = "Run manually - requires running server")]
    public void HealthEndpoint_Should_HandleHighLoad()
    {
        var scenario = Scenario.Create("health_check_load", async context =>
        {
            var request = Http.CreateRequest("GET", "/health")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(_client, request);

            return IsSuccessResponse(response)
                ? Response.Ok()
                : Response.Fail();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(rate: 10, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(5))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithTestSuite("HealthCheck")
            .WithTestName("HighLoad")
            .Run();

        // Assert
        var scenarioStats = stats.ScenarioStats[0];
        Assert.True(scenarioStats.Ok.Request.Percent >= 99,
            $"Health check success rate should be >= 99%, was {scenarioStats.Ok.Request.Percent}%");

        _output.WriteLine($"Health Check Load Test Results:");
        _output.WriteLine($"  Requests: {scenarioStats.Ok.Request.Count}");
        _output.WriteLine($"  Success Rate: {scenarioStats.Ok.Request.Percent}%");
        _output.WriteLine($"  Mean Latency: {scenarioStats.Ok.Latency.MeanMs}ms");
        _output.WriteLine($"  99th Percentile: {scenarioStats.Ok.Latency.Percent99}ms");
    }

    [Fact(Skip = "Run manually - requires running server")]
    public void CustomersEndpoint_Should_HandleConcurrentReads()
    {
        var scenario = Scenario.Create("get_customers_load", async context =>
        {
            var request = Http.CreateRequest("GET", "/api/v1/customers?page=1&pageSize=10")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(_client, request);

            return IsSuccessResponse(response)
                ? Response.Ok()
                : Response.Fail();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.KeepConstant(copies: 5, during: TimeSpan.FromSeconds(5))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithTestSuite("Customers")
            .WithTestName("ConcurrentReads")
            .Run();

        var scenarioStats = stats.ScenarioStats[0];
        Assert.True(scenarioStats.Ok.Request.Percent >= 95,
            $"Customers endpoint success rate should be >= 95%, was {scenarioStats.Ok.Request.Percent}%");

        _output.WriteLine($"Customers Concurrent Read Test Results:");
        _output.WriteLine($"  Requests: {scenarioStats.Ok.Request.Count}");
        _output.WriteLine($"  Success Rate: {scenarioStats.Ok.Request.Percent}%");
        _output.WriteLine($"  Mean Latency: {scenarioStats.Ok.Latency.MeanMs}ms");
        _output.WriteLine($"  RPS: {scenarioStats.Ok.Request.RPS}");
    }

    [Fact(Skip = "Run manually - requires running server")]
    public void ProductsEndpoint_Should_HandleConcurrentReads()
    {
        var scenario = Scenario.Create("get_products_load", async context =>
        {
            var request = Http.CreateRequest("GET", "/api/v1/products?page=1&pageSize=10")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(_client, request);

            return IsSuccessResponse(response)
                ? Response.Ok()
                : Response.Fail();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.KeepConstant(copies: 5, during: TimeSpan.FromSeconds(5))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithTestSuite("Products")
            .WithTestName("ConcurrentReads")
            .Run();

        var scenarioStats = stats.ScenarioStats[0];
        Assert.True(scenarioStats.Ok.Request.Percent >= 95,
            $"Products endpoint success rate should be >= 95%, was {scenarioStats.Ok.Request.Percent}%");

        _output.WriteLine($"Products Concurrent Read Test Results:");
        _output.WriteLine($"  Requests: {scenarioStats.Ok.Request.Count}");
        _output.WriteLine($"  Success Rate: {scenarioStats.Ok.Request.Percent}%");
        _output.WriteLine($"  Mean Latency: {scenarioStats.Ok.Latency.MeanMs}ms");
        _output.WriteLine($"  RPS: {scenarioStats.Ok.Request.RPS}");
    }

    [Fact(Skip = "Run manually - requires running server")]
    public void MixedWorkload_Should_HandleReadsAndWrites()
    {
        var counter = 0;

        var readScenario = Scenario.Create("mixed_read_customers", async context =>
        {
            var request = Http.CreateRequest("GET", "/api/v1/customers?page=1&pageSize=10")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(_client, request);

            return IsSuccessResponse(response)
                ? Response.Ok()
                : Response.Fail();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.KeepConstant(copies: 5, during: TimeSpan.FromSeconds(5))
        );

        var writeScenario = Scenario.Create("mixed_create_customer", async context =>
        {
            var uniqueId = Interlocked.Increment(ref counter);
            var payload = new
            {
                firstName = $"LoadTest{uniqueId}",
                lastName = $"User{uniqueId}",
                email = $"loadtest{uniqueId}@example.com"
            };

            var request = Http.CreateRequest("POST", "/api/v1/customers")
                .WithHeader("Accept", "application/json")
                .WithHeader("Content-Type", "application/json")
                .WithBody(new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"));

            var response = await Http.Send(_client, request);

            if (!FSharpOption<HttpResponseMessage>.get_IsSome(response.Payload))
                return Response.Fail();

            var statusCode = response.Payload.Value.StatusCode;
            return statusCode == System.Net.HttpStatusCode.Created ||
                   statusCode == System.Net.HttpStatusCode.OK
                ? Response.Ok()
                : Response.Fail();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(rate: 2, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(5))
        );

        var stats = NBomberRunner
            .RegisterScenarios(readScenario, writeScenario)
            .WithTestSuite("MixedWorkload")
            .WithTestName("ReadsAndWrites")
            .Run();

        foreach (var scenarioStats in stats.ScenarioStats)
        {
            _output.WriteLine($"{scenarioStats.ScenarioName} Results:");
            _output.WriteLine($"  Requests: {scenarioStats.Ok.Request.Count}");
            _output.WriteLine($"  Success Rate: {scenarioStats.Ok.Request.Percent}%");
            _output.WriteLine($"  Mean Latency: {scenarioStats.Ok.Latency.MeanMs}ms");
        }
    }

    [Fact(Skip = "Run manually - requires running server")]
    public void SpikeTest_Should_HandleTrafficSpike()
    {
        var scenario = Scenario.Create("spike_test", async context =>
        {
            var request = Http.CreateRequest("GET", "/api/v1/products?page=1&pageSize=10")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(_client, request);

            return IsSuccessResponse(response)
                ? Response.Ok()
                : Response.Fail();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            // Warm up
            Simulation.Inject(rate: 5, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(2)),
            // Spike
            Simulation.Inject(rate: 20, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(3)),
            // Cool down
            Simulation.Inject(rate: 5, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(2))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithTestSuite("SpikeTest")
            .WithTestName("TrafficSpike")
            .Run();

        var scenarioStats = stats.ScenarioStats[0];
        Assert.True(scenarioStats.Ok.Request.Percent >= 90,
            $"Spike test success rate should be >= 90%, was {scenarioStats.Ok.Request.Percent}%");

        _output.WriteLine($"Spike Test Results:");
        _output.WriteLine($"  Total Requests: {scenarioStats.Ok.Request.Count}");
        _output.WriteLine($"  Success Rate: {scenarioStats.Ok.Request.Percent}%");
        _output.WriteLine($"  Mean Latency: {scenarioStats.Ok.Latency.MeanMs}ms");
        _output.WriteLine($"  Max Latency: {scenarioStats.Ok.Latency.MaxMs}ms");
    }

    [Fact(Skip = "Run manually - requires running server")]
    public void StressTest_Should_FindBreakingPoint()
    {
        var scenario = Scenario.Create("stress_test", async context =>
        {
            var request = Http.CreateRequest("GET", "/health")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(_client, request);

            return IsSuccessResponse(response)
                ? Response.Ok()
                : Response.Fail();
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            // Gradually increase load
            Simulation.RampingInject(rate: 10, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(3)),
            Simulation.Inject(rate: 30, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(4)),
            Simulation.RampingInject(rate: 10, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(3))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithTestSuite("StressTest")
            .WithTestName("FindBreakingPoint")
            .Run();

        var scenarioStats = stats.ScenarioStats[0];

        _output.WriteLine($"Stress Test Results:");
        _output.WriteLine($"  Total Requests: {scenarioStats.Ok.Request.Count + scenarioStats.Fail.Request.Count}");
        _output.WriteLine($"  Success Rate: {scenarioStats.Ok.Request.Percent}%");
        _output.WriteLine($"  Failed Requests: {scenarioStats.Fail.Request.Count}");
        _output.WriteLine($"  Mean Latency: {scenarioStats.Ok.Latency.MeanMs}ms");
        _output.WriteLine($"  99th Percentile: {scenarioStats.Ok.Latency.Percent99}ms");
        _output.WriteLine($"  Max Latency: {scenarioStats.Ok.Latency.MaxMs}ms");
    }
}
