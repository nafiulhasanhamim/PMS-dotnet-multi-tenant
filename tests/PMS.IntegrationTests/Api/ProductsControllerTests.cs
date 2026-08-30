using System.Net;
using System.Net.Http.Json;
using PMS.Application.Common.DTOs;
using PMS.Application.Features.Products.Queries.GetProducts;
using PMS.Domain.Entities;
using PMS.Domain.ValueObjects;
using PMS.IntegrationTests.Common;
using PMS.WebApi.Controllers;
using FluentAssertions;
using Xunit;

namespace PMS.IntegrationTests.Api;

/// <summary>
/// Integration tests for ProductsController endpoints.
/// Tests the full HTTP request/response cycle including database operations.
/// </summary>
public class ProductsControllerTests : IntegrationTestBase
{
    private const string BaseUrl = "/api/products";

    public ProductsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    #region GET /api/products

    [Fact]
    public async Task GetProducts_WithNoData_ReturnsEmptyList()
    {
        // Act
        var response = await Client.GetAsync(BaseUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetProductsResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetProducts_WithSeededData_ReturnsProducts()
    {
        // Arrange
        await SeedProductsAsync(3);

        // Act
        var response = await Client.GetAsync(BaseUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetProductsResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetProducts_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        await SeedProductsAsync(15);

        // Act
        var response = await Client.GetAsync($"{BaseUrl}?page=2&pageSize=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetProductsResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(5);
        result.TotalCount.Should().Be(15);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(5);
    }

    [Fact]
    public async Task GetProducts_WithSearchTerm_FiltersResults()
    {
        // Arrange
        await SeedProductsAsync(5);
        var db = GetDbContext();
        var price = Money.Create(99.99m, "USD").Value;
        var specialProduct = new Product("Special Widget", "SPECIAL-001", price, "A special product");
        db.Products.Add(specialProduct);
        await db.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync($"{BaseUrl}?searchTerm=Special");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetProductsResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items.First().Name.Should().Be("Special Widget");
    }

    [Fact]
    public async Task GetProducts_WithPriceFilter_FiltersResults()
    {
        // Arrange
        var db = GetDbContext();
        var cheapPrice = Money.Create(10.00m, "USD").Value;
        var expensivePrice = Money.Create(100.00m, "USD").Value;

        db.Products.Add(new Product("Cheap Product", "CHEAP-001", cheapPrice, null));
        db.Products.Add(new Product("Expensive Product", "EXPENSIVE-001", expensivePrice, null));
        await db.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync($"{BaseUrl}?minPrice=50&maxPrice=150");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetProductsResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items.First().Name.Should().Be("Expensive Product");
    }

    #endregion

    #region GET /api/products/{id}

    [Fact]
    public async Task GetProduct_WithValidId_ReturnsProduct()
    {
        // Arrange
        var productId = await SeedSingleProductAsync("Test Product", "TEST-001", 49.99m);

        // Act
        var response = await Client.GetAsync($"{BaseUrl}/{productId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ProductDto>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(productId);
        result.Name.Should().Be("Test Product");
        result.Sku.Should().Be("TEST-001");
        result.Price.Should().Be(49.99m);
    }

    [Fact]
    public async Task GetProduct_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await Client.GetAsync($"{BaseUrl}/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region POST /api/products

    [Fact]
    public async Task CreateProduct_WithValidData_ReturnsCreatedProduct()
    {
        // Arrange
        var request = new CreateProductRequest(
            Name: "New Product",
            Sku: "NEW-001",
            Price: 29.99m,
            Currency: "USD",
            Description: "A brand new product",
            InitialStock: 100);

        // Act
        var response = await PostAsJsonAsync(BaseUrl, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<ProductDto>();
        result.Should().NotBeNull();
        result!.Id.Should().NotBeEmpty();
        result.Name.Should().Be("New Product");
        result.Sku.Should().Be("NEW-001");
        result.Price.Should().Be(29.99m);
        result.Currency.Should().Be("USD");
        result.StockQuantity.Should().Be(100);

        // Verify location header
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain(result.Id.ToString());
    }

    [Fact]
    public async Task CreateProduct_WithDuplicateSku_ReturnsConflict()
    {
        // Arrange
        await SeedSingleProductAsync("Existing Product", "DUPLICATE-SKU", 19.99m);

        var request = new CreateProductRequest(
            Name: "New Product",
            Sku: "DUPLICATE-SKU",
            Price: 39.99m);

        // Act
        var response = await PostAsJsonAsync(BaseUrl, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateProduct_WithEmptyName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateProductRequest(
            Name: "",
            Sku: "TEST-001",
            Price: 9.99m);

        // Act
        var response = await PostAsJsonAsync(BaseUrl, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateProduct_WithNegativePrice_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateProductRequest(
            Name: "Test Product",
            Sku: "TEST-001",
            Price: -10.00m);

        // Act
        var response = await PostAsJsonAsync(BaseUrl, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateProduct_WithZeroStock_CreatesProductWithZeroStock()
    {
        // Arrange
        var request = new CreateProductRequest(
            Name: "Zero Stock Product",
            Sku: "ZERO-001",
            Price: 15.00m,
            InitialStock: 0);

        // Act
        var response = await PostAsJsonAsync(BaseUrl, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<ProductDto>();
        result!.StockQuantity.Should().Be(0);
    }

    #endregion

    #region PUT /api/products/{id}

    [Fact]
    public async Task UpdateProduct_WithValidData_ReturnsUpdatedProduct()
    {
        // Arrange
        var productId = await SeedSingleProductAsync("Original Product", "ORIG-001", 25.00m);

        var request = new UpdateProductRequest(
            Name: "Updated Product",
            Sku: "UPDATED-001",
            Price: 35.00m,
            Currency: "USD",
            Description: "Updated description");

        // Act
        var response = await PutAsJsonAsync($"{BaseUrl}/{productId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ProductDto>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Product");
        result.Sku.Should().Be("UPDATED-001");
        result.Price.Should().Be(35.00m);
        result.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task UpdateProduct_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateProductRequest(
            Name: "Test Product",
            Sku: "TEST-001",
            Price: 10.00m);

        // Act
        var response = await PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region DELETE /api/products/{id}

    [Fact]
    public async Task DeleteProduct_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var productId = await SeedSingleProductAsync("ToDelete Product", "DELETE-001", 5.00m);

        // Act
        var response = await Client.DeleteAsync($"{BaseUrl}/{productId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify product is soft-deleted (not returned in GET)
        var getResponse = await Client.GetAsync($"{BaseUrl}/{productId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteProduct_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await Client.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Helper Methods

    private async Task SeedProductsAsync(int count)
    {
        var db = GetDbContext();

        for (int i = 1; i <= count; i++)
        {
            var price = Money.Create(10.00m * i, "USD").Value;
            var product = new Product($"Product {i}", $"SKU-{i:D3}", price, $"Description for product {i}");
            db.Products.Add(product);
        }

        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedSingleProductAsync(string name, string sku, decimal price)
    {
        var db = GetDbContext();
        var priceObj = Money.Create(price, "USD").Value;
        var product = new Product(name, sku, priceObj, null);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    #endregion
}
