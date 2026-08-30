using PMS.Domain.Entities;
using PMS.Domain.ValueObjects;
using PMS.SchemaTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PMS.SchemaTests;

/// <summary>
/// Tests to verify query filters are correctly configured.
/// Validates soft delete filters and their behavior.
/// </summary>
public class QueryFilterTests : IClassFixture<SchemaTestFixture>
{
    private readonly SchemaTestFixture _fixture;

    public QueryFilterTests(SchemaTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Soft Delete Filter Tests

    [Theory]
    [InlineData(typeof(Customer))]
    [InlineData(typeof(Product))]
    [InlineData(typeof(Order))]
    public void SoftDeleteEntity_Should_HaveQueryFilter(Type entityType)
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(entityType);

        // Assert
        entity.Should().NotBeNull();
        entity!.GetQueryFilter().Should().NotBeNull(
            $"{entityType.Name} should have a soft delete query filter");
    }

    [Fact]
    public void AccessLog_Should_NotHaveQueryFilter()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(AccessLog));

        // Assert - AccessLog doesn't implement ISoftDelete
        entity.Should().NotBeNull();
        entity!.GetQueryFilter().Should().BeNull(
            "AccessLog should not have a query filter as it's not soft-deletable");
    }

    [Fact]
    public void SoftDeleteFilter_Should_ExcludeDeletedCustomers()
    {
        // Arrange
        var email1 = Email.Create("active@example.com").Value;
        var email2 = Email.Create("deleted@example.com").Value;

        var activeCustomer = new Customer("Active", "Customer", email1);
        var deletedCustomer = new Customer("Deleted", "Customer", email2);

        // Use reflection to set IsDeleted (normally done through Delete() method)
        var isDeletedProperty = typeof(Customer).GetProperty("IsDeleted");
        isDeletedProperty?.SetValue(deletedCustomer, true);

        _fixture.Context.Customers.AddRange(activeCustomer, deletedCustomer);
        _fixture.Context.SaveChanges();

        // Act
        var customers = _fixture.Context.Customers.ToList();
        var allCustomers = _fixture.Context.Customers.IgnoreQueryFilters().ToList();

        // Assert
        customers.Should().ContainSingle(c => c.Email.Value == "active@example.com");
        customers.Should().NotContain(c => c.Email.Value == "deleted@example.com");
        allCustomers.Should().HaveCount(2);

        // Cleanup
        _fixture.Context.Customers.RemoveRange(
            _fixture.Context.Customers.IgnoreQueryFilters());
        _fixture.Context.SaveChanges();
    }

    [Fact]
    public void SoftDeleteFilter_Should_ExcludeDeletedProducts()
    {
        // Arrange
        var price = Money.Create(100, "USD").Value;

        var activeProduct = new Product("Active Product", "SKU-ACTIVE", price);
        var deletedProduct = new Product("Deleted Product", "SKU-DELETED", price);

        // Use reflection to set IsDeleted
        var isDeletedProperty = typeof(Product).GetProperty("IsDeleted");
        isDeletedProperty?.SetValue(deletedProduct, true);

        _fixture.Context.Products.AddRange(activeProduct, deletedProduct);
        _fixture.Context.SaveChanges();

        // Act
        var products = _fixture.Context.Products.ToList();
        var allProducts = _fixture.Context.Products.IgnoreQueryFilters().ToList();

        // Assert
        products.Should().ContainSingle(p => p.Sku == "SKU-ACTIVE");
        products.Should().NotContain(p => p.Sku == "SKU-DELETED");
        allProducts.Should().HaveCount(2);

        // Cleanup
        _fixture.Context.Products.RemoveRange(
            _fixture.Context.Products.IgnoreQueryFilters());
        _fixture.Context.SaveChanges();
    }

    #endregion

    #region IgnoreQueryFilters Tests

    [Fact]
    public void IgnoreQueryFilters_Should_ReturnAllEntities()
    {
        // Arrange
        var email = Email.Create("filtertest@example.com").Value;
        var customer = new Customer("Filter", "Test", email);

        // Set as deleted
        var isDeletedProperty = typeof(Customer).GetProperty("IsDeleted");
        isDeletedProperty?.SetValue(customer, true);

        _fixture.Context.Customers.Add(customer);
        _fixture.Context.SaveChanges();

        // Act
        var filtered = _fixture.Context.Customers
            .Where(c => c.Email.Value == "filtertest@example.com")
            .ToList();

        var unfiltered = _fixture.Context.Customers
            .IgnoreQueryFilters()
            .Where(c => c.Email.Value == "filtertest@example.com")
            .ToList();

        // Assert
        filtered.Should().BeEmpty("Filtered query should exclude deleted entities");
        unfiltered.Should().ContainSingle("Unfiltered query should include deleted entities");

        // Cleanup
        _fixture.Context.Customers.RemoveRange(
            _fixture.Context.Customers.IgnoreQueryFilters()
                .Where(c => c.Email.Value == "filtertest@example.com"));
        _fixture.Context.SaveChanges();
    }

    #endregion
}
