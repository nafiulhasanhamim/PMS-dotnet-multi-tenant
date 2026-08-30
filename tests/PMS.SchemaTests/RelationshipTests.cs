using PMS.Domain.Entities;
using PMS.SchemaTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PMS.SchemaTests;

/// <summary>
/// Tests to verify EF Core relationship configurations are correct.
/// Validates foreign keys, navigation properties, and cascade behaviors.
/// </summary>
public class RelationshipTests : IClassFixture<SchemaTestFixture>
{
    private readonly SchemaTestFixture _fixture;

    public RelationshipTests(SchemaTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Customer-Order Relationship Tests

    [Fact]
    public void Customer_Should_HaveOrdersNavigation()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(Customer));
        var navigation = entity?.FindNavigation("Orders");

        // Assert
        navigation.Should().NotBeNull("Customer should have Orders navigation");
        navigation!.IsCollection.Should().BeTrue();
    }

    [Fact]
    public void Order_Should_HaveCustomerNavigation()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(Order));
        var navigation = entity?.FindNavigation("Customer");

        // Assert
        navigation.Should().NotBeNull("Order should have Customer navigation");
        navigation!.IsCollection.Should().BeFalse();
    }

    [Fact]
    public void Order_Should_HaveCustomerIdForeignKey()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(Order));
        var foreignKeys = entity?.GetForeignKeys().ToList();

        // Assert
        foreignKeys.Should().Contain(fk =>
            fk.Properties.Any(p => p.Name == "CustomerId") &&
            fk.PrincipalEntityType.ClrType == typeof(Customer));
    }

    [Fact]
    public void CustomerOrder_Should_HaveRestrictDeleteBehavior()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(Order));
        var foreignKey = entity?.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(Customer));

        // Assert
        foreignKey.Should().NotBeNull();
        foreignKey!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict,
            "Deleting a customer should not cascade delete orders");
    }

    #endregion

    #region Order-OrderItem Relationship Tests

    [Fact]
    public void Order_Should_HaveItemsNavigation()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(Order));
        var navigation = entity?.FindNavigation("Items");

        // Assert
        navigation.Should().NotBeNull("Order should have Items navigation");
        navigation!.IsCollection.Should().BeTrue();
    }

    [Fact]
    public void OrderItem_Should_HaveOrderIdForeignKey()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(OrderItem));
        var foreignKeys = entity?.GetForeignKeys().ToList();

        // Assert
        foreignKeys.Should().Contain(fk =>
            fk.Properties.Any(p => p.Name == "OrderId") &&
            fk.PrincipalEntityType.ClrType == typeof(Order));
    }

    [Fact]
    public void OrderOrderItem_Should_HaveCascadeDeleteBehavior()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(OrderItem));
        var foreignKey = entity?.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(Order));

        // Assert
        foreignKey.Should().NotBeNull();
        foreignKey!.DeleteBehavior.Should().Be(DeleteBehavior.Cascade,
            "Deleting an order should cascade delete its items");
    }

    #endregion

    #region OrderItem-Product Relationship Tests

    [Fact]
    public void OrderItem_Should_HaveProductIdProperty()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(OrderItem));
        var productIdProperty = entity?.FindProperty("ProductId");

        // Assert - ProductId is stored as a denormalized reference (no FK constraint)
        // This is by design to preserve order history even if products are deleted
        productIdProperty.Should().NotBeNull("OrderItem should have ProductId property");
        productIdProperty!.ClrType.Should().Be(typeof(Guid));
        productIdProperty.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void OrderItem_Should_HaveProductIdIndex()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(OrderItem));
        var indexes = entity?.GetIndexes().ToList();

        // Assert
        indexes.Should().Contain(i =>
            i.Properties.Any(p => p.Name == "ProductId"),
            "OrderItem should have an index on ProductId for query performance");
    }

    #endregion

    #region Foreign Key Count Tests

    [Fact]
    public void Customer_Should_HaveNoForeignKeys()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(Customer));
        var foreignKeys = entity?.GetForeignKeys().ToList();

        // Assert - Customer is a root aggregate
        foreignKeys.Should().BeEmpty("Customer is a root aggregate with no foreign keys");
    }

    [Fact]
    public void Product_Should_HaveNoForeignKeys()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(Product));
        var foreignKeys = entity?.GetForeignKeys().ToList();

        // Assert - Product is a root aggregate
        foreignKeys.Should().BeEmpty("Product is a root aggregate with no foreign keys");
    }

    [Fact]
    public void Order_Should_HaveOneForeignKey()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(Order));
        var foreignKeys = entity?.GetForeignKeys()
            .Where(fk => fk.PrincipalEntityType.ClrType != typeof(Order)) // Exclude owned types
            .ToList();

        // Assert - Order references Customer
        foreignKeys.Should().HaveCount(1);
        foreignKeys![0].PrincipalEntityType.ClrType.Should().Be(typeof(Customer));
    }

    [Fact]
    public void OrderItem_Should_HaveOneForeignKey()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(OrderItem));
        var foreignKeys = entity?.GetForeignKeys().ToList();

        // Assert - OrderItem only has FK to Order
        // ProductId is a denormalized reference without FK constraint
        foreignKeys.Should().HaveCount(1);
        foreignKeys![0].PrincipalEntityType.ClrType.Should().Be(typeof(Order));
    }

    #endregion
}
