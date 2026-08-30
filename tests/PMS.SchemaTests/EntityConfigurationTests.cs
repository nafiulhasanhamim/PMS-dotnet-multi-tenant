using PMS.Domain.Entities;
using PMS.SchemaTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PMS.SchemaTests;

/// <summary>
/// Tests to verify EF Core entity configurations are applied correctly.
/// Validates table names, column configurations, indexes, and relationships.
/// </summary>
public class EntityConfigurationTests : IClassFixture<SchemaTestFixture>
{
    private readonly SchemaTestFixture _fixture;

    public EntityConfigurationTests(SchemaTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Table Name Tests

    [Theory]
    [InlineData(typeof(Customer), "Customers")]
    [InlineData(typeof(Product), "Products")]
    [InlineData(typeof(Order), "Orders")]
    [InlineData(typeof(OrderItem), "OrderItems")]
    [InlineData(typeof(AccessLog), "AccessLogs")]
    public void Entity_Should_MapToCorrectTable(Type entityType, string expectedTableName)
    {
        // Arrange
        var entityTypeMetadata = _fixture.Model.FindEntityType(entityType);

        // Assert
        entityTypeMetadata.Should().NotBeNull($"Entity {entityType.Name} should be registered");
        entityTypeMetadata!.GetTableName().Should().Be(expectedTableName);
    }

    #endregion

    #region Customer Configuration Tests

    [Fact]
    public void Customer_Should_HaveCorrectPrimaryKey()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(Customer));
        var primaryKey = entity?.FindPrimaryKey();

        // Assert
        primaryKey.Should().NotBeNull();
        primaryKey!.Properties.Should().ContainSingle(p => p.Name == "Id");
    }

    [Theory]
    [InlineData("FirstName", 100, false)]
    [InlineData("LastName", 100, false)]
    [InlineData("Status", 20, false)]
    [InlineData("CreatedBy", 256, true)]
    [InlineData("ModifiedBy", 256, true)]
    [InlineData("DeletedBy", 256, true)]
    public void Customer_Should_HaveCorrectColumnConfiguration(
        string propertyName, int maxLength, bool isNullable)
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(Customer));
        var property = entity?.FindProperty(propertyName);

        // Assert
        property.Should().NotBeNull($"Property {propertyName} should exist");
        property!.GetMaxLength().Should().Be(maxLength);
        property.IsNullable.Should().Be(isNullable);
    }

    [Fact]
    public void Customer_Email_Should_BeConfiguredAsOwnedType()
    {
        // Arrange
        var customer = _fixture.Model.FindEntityType(typeof(Customer));

        // Find the owned Email type via navigation
        var emailNavigation = customer?.FindNavigation("Email");

        // Assert
        emailNavigation.Should().NotBeNull("Customer should have Email navigation");
        emailNavigation!.TargetEntityType.IsOwned().Should().BeTrue("Email should be an owned type");

        // Check the Email.Value property on the owned type
        var emailValueProperty = emailNavigation.TargetEntityType.FindProperty("Value");
        emailValueProperty.Should().NotBeNull("Email owned type should have Value property");
        emailValueProperty!.GetMaxLength().Should().Be(256); // Email.MaxLength
    }

    [Fact]
    public void Customer_Email_Should_HaveUniqueIndex()
    {
        // Arrange
        var customer = _fixture.Model.FindEntityType(typeof(Customer));

        // Find the owned Email type
        var ownedTypes = customer?.GetNavigations()
            .Where(n => n.TargetEntityType.IsOwned() && n.Name == "Email")
            .Select(n => n.TargetEntityType)
            .ToList();

        // Assert - Email owned type should exist and have unique index
        ownedTypes.Should().NotBeEmpty();
        var emailType = ownedTypes![0];
        var indexes = emailType.GetIndexes().ToList();
        indexes.Should().Contain(i => i.IsUnique, "Email should have a unique index");
    }

    [Fact]
    public void Customer_Should_HaveIndexOnLastName()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(Customer));
        var indexes = entity?.GetIndexes().ToList();

        // Assert
        indexes.Should().Contain(i =>
            i.Properties.Any(p => p.Name == "LastName"));
    }

    [Fact]
    public void Customer_Should_HaveIndexOnStatus()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(Customer));
        var indexes = entity?.GetIndexes().ToList();

        // Assert
        indexes.Should().Contain(i =>
            i.Properties.Any(p => p.Name == "Status"));
    }

    [Fact]
    public void Customer_Should_HaveIndexOnIsDeleted()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(Customer));
        var indexes = entity?.GetIndexes().ToList();

        // Assert
        indexes.Should().Contain(i =>
            i.Properties.Any(p => p.Name == "IsDeleted"));
    }

    #endregion

    #region Product Configuration Tests

    [Fact]
    public void Product_Should_HaveCorrectPrimaryKey()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(Product));
        var primaryKey = entity?.FindPrimaryKey();

        // Assert
        primaryKey.Should().NotBeNull();
        primaryKey!.Properties.Should().ContainSingle(p => p.Name == "Id");
    }

    [Theory]
    [InlineData("Name", 200, false)]
    [InlineData("Description", 2000, true)]
    [InlineData("Sku", 50, false)]
    public void Product_Should_HaveCorrectColumnConfiguration(
        string propertyName, int maxLength, bool isNullable)
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(Product));
        var property = entity?.FindProperty(propertyName);

        // Assert
        property.Should().NotBeNull($"Property {propertyName} should exist");
        property!.GetMaxLength().Should().Be(maxLength);
        property.IsNullable.Should().Be(isNullable);
    }

    [Fact]
    public void Product_Should_HaveUniqueSkuIndex()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(Product));
        var indexes = entity?.GetIndexes().ToList();

        // Assert
        indexes.Should().Contain(i =>
            i.IsUnique && i.Properties.Any(p => p.Name == "Sku"));
    }

    [Fact]
    public void Product_Price_Should_BeConfiguredAsOwnedType()
    {
        // Arrange
        var product = _fixture.Model.FindEntityType(typeof(Product));

        // Find the owned Price type via navigation
        var priceNavigation = product?.FindNavigation("Price");

        // Assert
        priceNavigation.Should().NotBeNull("Product should have Price navigation");
        priceNavigation!.TargetEntityType.IsOwned().Should().BeTrue("Price should be an owned type");

        // Check the Amount property on the owned type
        var amountProperty = priceNavigation.TargetEntityType.FindProperty("Amount");
        amountProperty.Should().NotBeNull("Price owned type should have Amount property");
        amountProperty!.GetPrecision().Should().Be(18);
        amountProperty.GetScale().Should().Be(2);
    }

    #endregion

    #region Order Configuration Tests

    [Fact]
    public void Order_Should_HaveCorrectPrimaryKey()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(Order));
        var primaryKey = entity?.FindPrimaryKey();

        // Assert
        primaryKey.Should().NotBeNull();
        primaryKey!.Properties.Should().ContainSingle(p => p.Name == "Id");
    }

    [Theory]
    [InlineData("OrderNumber", 50, false)]
    [InlineData("Status", 20, false)]
    [InlineData("Notes", 2000, true)]
    public void Order_Should_HaveCorrectColumnConfiguration(
        string propertyName, int maxLength, bool isNullable)
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(Order));
        var property = entity?.FindProperty(propertyName);

        // Assert
        property.Should().NotBeNull($"Property {propertyName} should exist");
        property!.GetMaxLength().Should().Be(maxLength);
        property.IsNullable.Should().Be(isNullable);
    }

    [Fact]
    public void Order_Should_HaveUniqueOrderNumberIndex()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(Order));
        var indexes = entity?.GetIndexes().ToList();

        // Assert
        indexes.Should().Contain(i =>
            i.IsUnique && i.Properties.Any(p => p.Name == "OrderNumber"));
    }

    [Fact]
    public void Order_Should_HaveIndexOnCustomerId()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(Order));
        var indexes = entity?.GetIndexes().ToList();

        // Assert
        indexes.Should().Contain(i =>
            i.Properties.Any(p => p.Name == "CustomerId"));
    }

    [Fact]
    public void Order_ShippingAddress_Should_BeConfiguredAsOwnedType()
    {
        // Arrange
        var order = _fixture.Model.FindEntityType(typeof(Order));

        // Find the owned ShippingAddress type via navigation
        var addressNavigation = order?.FindNavigation("ShippingAddress");

        // Assert
        addressNavigation.Should().NotBeNull("Order should have ShippingAddress navigation");
        addressNavigation!.TargetEntityType.IsOwned().Should().BeTrue("ShippingAddress should be an owned type");

        // Check that Street is required
        var streetProperty = addressNavigation.TargetEntityType.FindProperty("Street");
        streetProperty.Should().NotBeNull("ShippingAddress should have Street property");
        streetProperty!.IsNullable.Should().BeFalse("ShippingAddress.Street should be required");
    }

    #endregion

    #region OrderItem Configuration Tests

    [Fact]
    public void OrderItem_Should_HaveCorrectPrimaryKey()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(OrderItem));
        var primaryKey = entity?.FindPrimaryKey();

        // Assert
        primaryKey.Should().NotBeNull();
        primaryKey!.Properties.Should().ContainSingle(p => p.Name == "Id");
    }

    [Fact]
    public void OrderItem_Should_HaveIndexOnOrderId()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(OrderItem));
        var indexes = entity?.GetIndexes().ToList();

        // Assert
        indexes.Should().Contain(i =>
            i.Properties.Any(p => p.Name == "OrderId"));
    }

    [Fact]
    public void OrderItem_Should_HaveIndexOnProductId()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(OrderItem));
        var indexes = entity?.GetIndexes().ToList();

        // Assert
        indexes.Should().Contain(i =>
            i.Properties.Any(p => p.Name == "ProductId"));
    }

    #endregion

    #region AccessLog Configuration Tests

    [Fact]
    public void AccessLog_Should_HaveCorrectTableName()
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(AccessLog));

        // Assert
        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("AccessLogs");
    }

    [Theory]
    [InlineData("EntityName", 100, false)]
    [InlineData("Action", 50, false)]
    [InlineData("UserId", 450, true)]
    public void AccessLog_Should_HaveCorrectColumnConfiguration(
        string propertyName, int maxLength, bool isNullable)
    {
        // Arrange
        var entity = _fixture.Model.FindEntityType(typeof(AccessLog));
        var property = entity?.FindProperty(propertyName);

        // Assert
        property.Should().NotBeNull($"Property {propertyName} should exist");
        property!.GetMaxLength().Should().Be(maxLength);
        property.IsNullable.Should().Be(isNullable);
    }

    #endregion
}
