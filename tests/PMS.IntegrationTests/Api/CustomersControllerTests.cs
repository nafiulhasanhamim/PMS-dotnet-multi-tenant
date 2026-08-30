using System.Net;
using System.Net.Http.Json;
using PMS.Application.Common.DTOs;
using PMS.Application.Features.Customers.Queries.GetCustomers;
using PMS.Domain.Entities;
using PMS.Domain.ValueObjects;
using PMS.IntegrationTests.Common;
using PMS.WebApi.Controllers;
using FluentAssertions;
using Xunit;

namespace PMS.IntegrationTests.Api;

/// <summary>
/// Integration tests for CustomersController endpoints.
/// Tests the full HTTP request/response cycle including database operations.
/// </summary>
public class CustomersControllerTests : IntegrationTestBase
{
    private const string BaseUrl = "/api/customers";

    public CustomersControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    #region GET /api/customers

    [Fact]
    public async Task GetCustomers_WithNoData_ReturnsEmptyList()
    {
        // Act
        var response = await Client.GetAsync(BaseUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetCustomersResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetCustomers_WithSeededData_ReturnsCustomers()
    {
        // Arrange
        await SeedCustomersAsync(3);

        // Act
        var response = await Client.GetAsync(BaseUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetCustomersResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetCustomers_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        await SeedCustomersAsync(15);

        // Act
        var response = await Client.GetAsync($"{BaseUrl}?page=2&pageSize=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetCustomersResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(5);
        result.TotalCount.Should().Be(15);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(5);
    }

    [Fact]
    public async Task GetCustomers_WithSearchTerm_FiltersResults()
    {
        // Arrange
        await SeedCustomersAsync(5);
        var db = GetDbContext();
        var specialCustomer = new Customer("Special", "Customer", Email.Create("special@example.com").Value);
        db.Customers.Add(specialCustomer);
        await db.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync($"{BaseUrl}?searchTerm=Special");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetCustomersResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items.First().FullName.Should().Contain("Special");
    }

    #endregion

    #region GET /api/customers/{id}

    [Fact]
    public async Task GetCustomer_WithValidId_ReturnsCustomer()
    {
        // Arrange
        var customerId = await SeedSingleCustomerAsync("John", "Doe", "john@example.com");

        // Act
        var response = await Client.GetAsync($"{BaseUrl}/{customerId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CustomerDto>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(customerId);
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
        result.Email.Should().Be("john@example.com");
    }

    [Fact]
    public async Task GetCustomer_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region POST /api/customers

    [Fact]
    public async Task CreateCustomer_WithValidData_ReturnsCreatedCustomer()
    {
        // Arrange
        var request = new CreateCustomerRequest(
            FirstName: "Jane",
            LastName: "Smith",
            Email: "jane.smith@example.com",
            PhoneNumber: "+1-555-123-4567");

        // Act
        var response = await PostAsJsonAsync(BaseUrl, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CustomerDto>();
        result.Should().NotBeNull();
        result!.Id.Should().NotBeEmpty();
        result.FirstName.Should().Be("Jane");
        result.LastName.Should().Be("Smith");
        result.Email.Should().Be("jane.smith@example.com");

        // Verify location header
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain(result.Id.ToString());
    }

    [Fact]
    public async Task CreateCustomer_WithDuplicateEmail_ReturnsConflict()
    {
        // Arrange
        await SeedSingleCustomerAsync("Existing", "User", "duplicate@example.com");

        var request = new CreateCustomerRequest(
            FirstName: "New",
            LastName: "User",
            Email: "duplicate@example.com");

        // Act
        var response = await PostAsJsonAsync(BaseUrl, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateCustomer_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateCustomerRequest(
            FirstName: "Test",
            LastName: "User",
            Email: "invalid-email");

        // Act
        var response = await PostAsJsonAsync(BaseUrl, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCustomer_WithEmptyFirstName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateCustomerRequest(
            FirstName: "",
            LastName: "User",
            Email: "test@example.com");

        // Act
        var response = await PostAsJsonAsync(BaseUrl, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region PUT /api/customers/{id}

    [Fact]
    public async Task UpdateCustomer_WithValidData_ReturnsUpdatedCustomer()
    {
        // Arrange
        var customerId = await SeedSingleCustomerAsync("Original", "Name", "original@example.com");

        var request = new UpdateCustomerRequest(
            FirstName: "Updated",
            LastName: "Customer",
            Email: "updated@example.com");

        // Act
        var response = await PutAsJsonAsync($"{BaseUrl}/{customerId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CustomerDto>();
        result.Should().NotBeNull();
        result!.FirstName.Should().Be("Updated");
        result.LastName.Should().Be("Customer");
        result.Email.Should().Be("updated@example.com");
    }

    [Fact]
    public async Task UpdateCustomer_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateCustomerRequest(
            FirstName: "Test",
            LastName: "User",
            Email: "test@example.com");

        // Act
        var response = await PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region DELETE /api/customers/{id}

    [Fact]
    public async Task DeleteCustomer_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var customerId = await SeedSingleCustomerAsync("ToDelete", "User", "delete@example.com");

        // Act
        var response = await Client.DeleteAsync($"{BaseUrl}/{customerId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify customer is soft-deleted (not returned in GET)
        var getResponse = await Client.GetAsync($"{BaseUrl}/{customerId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCustomer_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await Client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Helper Methods

    private async Task SeedCustomersAsync(int count)
    {
        var db = GetDbContext();

        for (int i = 1; i <= count; i++)
        {
            var email = Email.Create($"customer{i}@example.com").Value;
            var customer = new Customer($"FirstName{i}", $"LastName{i}", email);
            db.Customers.Add(customer);
        }

        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedSingleCustomerAsync(string firstName, string lastName, string email)
    {
        var db = GetDbContext();
        var emailObj = Email.Create(email).Value;
        var customer = new Customer(firstName, lastName, emailObj);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer.Id;
    }

    #endregion
}
