using PMS.Domain.Entities;
using PMS.SchemaTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PMS.SchemaTests;

/// <summary>
/// Tests to verify EF Core model consistency and completeness.
/// Validates that all expected entities are mapped and configurations are applied.
/// </summary>
public class ModelConsistencyTests : IClassFixture<SchemaTestFixture>
{
    private readonly SchemaTestFixture _fixture;

    public ModelConsistencyTests(SchemaTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Entity Registration Tests

    [Fact]
    public void Model_Should_ContainAllExpectedEntities()
    {
        // Arrange
        var expectedEntities = new[]
        {
            typeof(Customer),
            typeof(Product),
            typeof(Order),
            typeof(OrderItem),
            typeof(AccessLog)
        };

        // Act & Assert
        foreach (var entityType in expectedEntities)
        {
            var entity = _fixture.Model.FindEntityType(entityType);
            entity.Should().NotBeNull($"Entity {entityType.Name} should be registered in the model");
        }
    }

    [Fact]
    public void Model_Should_HaveExpectedEntityCount()
    {
        // Arrange - Count only non-owned entity types
        var entityTypes = _fixture.Model.GetEntityTypes()
            .Where(e => !e.IsOwned())
            .ToList();

        // Assert - 5 main entities (Customer, Product, Order, OrderItem, AccessLog)
        entityTypes.Should().HaveCountGreaterThanOrEqualTo(5,
            "Model should have at least 5 main entity types");
    }

    #endregion

    #region Primary Key Tests

    [Fact]
    public void AllEntities_Should_HavePrimaryKey()
    {
        // Arrange
        var entityTypes = _fixture.Model.GetEntityTypes()
            .Where(e => !e.IsOwned())
            .ToList();

        // Assert
        foreach (var entity in entityTypes)
        {
            var primaryKey = entity.FindPrimaryKey();
            primaryKey.Should().NotBeNull(
                $"Entity {entity.ClrType.Name} should have a primary key");
        }
    }

    [Fact]
    public void AllEntities_Should_UseGuidPrimaryKey()
    {
        // Arrange
        var mainEntities = new[]
        {
            typeof(Customer),
            typeof(Product),
            typeof(Order),
            typeof(OrderItem),
            typeof(AccessLog)
        };

        // Assert
        foreach (var entityType in mainEntities)
        {
            var entity = _fixture.Model.FindEntityType(entityType);
            var primaryKey = entity?.FindPrimaryKey();

            primaryKey.Should().NotBeNull();
            primaryKey!.Properties.Should().ContainSingle();
            primaryKey.Properties[0].ClrType.Should().Be(typeof(Guid),
                $"{entityType.Name} should use Guid for primary key");
        }
    }

    #endregion

    #region Owned Type Tests

    [Fact]
    public void Customer_Should_HaveOwnedEmail()
    {
        // Arrange
        var customer = _fixture.Model.FindEntityType(typeof(Customer));
        var ownedTypes = customer?.GetNavigations()
            .Where(n => n.TargetEntityType.IsOwned())
            .ToList();

        // Assert
        ownedTypes.Should().Contain(n => n.Name == "Email");
    }

    [Fact]
    public void Customer_Should_HaveOwnedPhoneNumber()
    {
        // Arrange
        var customer = _fixture.Model.FindEntityType(typeof(Customer));
        var ownedTypes = customer?.GetNavigations()
            .Where(n => n.TargetEntityType.IsOwned())
            .ToList();

        // Assert
        ownedTypes.Should().Contain(n => n.Name == "PhoneNumber");
    }

    [Fact]
    public void Customer_Should_HaveOwnedShippingAddress()
    {
        // Arrange
        var customer = _fixture.Model.FindEntityType(typeof(Customer));
        var ownedTypes = customer?.GetNavigations()
            .Where(n => n.TargetEntityType.IsOwned())
            .ToList();

        // Assert
        ownedTypes.Should().Contain(n => n.Name == "ShippingAddress");
    }

    [Fact]
    public void Product_Should_HaveOwnedPrice()
    {
        // Arrange
        var product = _fixture.Model.FindEntityType(typeof(Product));
        var ownedTypes = product?.GetNavigations()
            .Where(n => n.TargetEntityType.IsOwned())
            .ToList();

        // Assert
        ownedTypes.Should().Contain(n => n.Name == "Price");
    }

    [Fact]
    public void Order_Should_HaveOwnedShippingAddress()
    {
        // Arrange
        var order = _fixture.Model.FindEntityType(typeof(Order));
        var ownedTypes = order?.GetNavigations()
            .Where(n => n.TargetEntityType.IsOwned())
            .ToList();

        // Assert
        ownedTypes.Should().Contain(n => n.Name == "ShippingAddress");
    }

    #endregion

    #region Enum Configuration Tests

    [Fact]
    public void CustomerStatus_Should_HaveMaxLength()
    {
        // Arrange
        var customer = _fixture.Model.FindEntityType(typeof(Customer));
        var statusProperty = customer?.FindProperty("Status");

        // Assert
        statusProperty.Should().NotBeNull();
        // Status configured with HasMaxLength(20) indicates string storage
        statusProperty!.GetMaxLength().Should().Be(20,
            "CustomerStatus should have max length of 20 indicating string storage");
    }

    [Fact]
    public void OrderStatus_Should_HaveMaxLength()
    {
        // Arrange
        var order = _fixture.Model.FindEntityType(typeof(Order));
        var statusProperty = order?.FindProperty("Status");

        // Assert
        statusProperty.Should().NotBeNull();
        // Status configured with HasMaxLength(20) indicates string storage
        statusProperty!.GetMaxLength().Should().Be(20,
            "OrderStatus should have max length of 20 indicating string storage");
    }

    #endregion

    #region Index Count Tests

    [Fact]
    public void Customer_Should_HaveExpectedIndexCount()
    {
        // Arrange
        var customer = _fixture.Model.FindEntityType(typeof(Customer));
        var indexes = customer?.GetIndexes().ToList();

        // Assert - 3 indexes on main entity: LastName, Status, IsDeleted
        // Email unique index is on the owned Email type
        indexes.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Product_Should_HaveExpectedIndexCount()
    {
        // Arrange
        var product = _fixture.Model.FindEntityType(typeof(Product));
        var indexes = product?.GetIndexes().ToList();

        // Assert - At least 4 indexes: Sku (unique), Name, IsActive, IsDeleted
        indexes.Should().HaveCountGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void Order_Should_HaveExpectedIndexCount()
    {
        // Arrange
        var order = _fixture.Model.FindEntityType(typeof(Order));
        var indexes = order?.GetIndexes().ToList();

        // Assert - At least 5 indexes: OrderNumber (unique), CustomerId, OrderDateUtc, Status, IsDeleted
        indexes.Should().HaveCountGreaterThanOrEqualTo(5);
    }

    #endregion

    #region Database Can Be Created Tests

    [Fact]
    public void Model_Should_BeAbleToCreateDatabase()
    {
        // Act - This is implicitly tested by the fixture, but let's be explicit
        var canCreate = _fixture.Context.Database.EnsureCreated();

        // Assert - Database should already exist from fixture setup
        // If this were a fresh context, canCreate would be true
        canCreate.Should().BeFalse("Database should already exist from fixture");
    }

    [Fact]
    public void Model_Should_HaveNoValidationErrors()
    {
        // Arrange & Act
        var model = _fixture.Model;

        // Assert - If we got here without exceptions, the model is valid
        model.Should().NotBeNull();
        model.GetEntityTypes().Should().NotBeEmpty();
    }

    #endregion
}
