using PMS.Application.Features.Products.Commands.CreateProduct;
using FluentAssertions;
using Xunit;

namespace PMS.UnitTests.Application.Products.Commands;

public partial class CreateProductCommandTests
{
    public class ValidatorTests
    {
        private readonly CreateProductCommandValidator _validator;

        public ValidatorTests()
        {
            _validator = new CreateProductCommandValidator();
        }

        #region Valid Commands

        [Fact]
        public void Validate_ValidCommand_ShouldPass()
        {
            // Arrange
            var command = CreateValidCommand();

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void Validate_ValidCommandWithDescription_ShouldPass()
        {
            // Arrange
            var command = CreateValidCommand() with { Description = "A detailed product description" };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_ValidCommandWithInitialStock_ShouldPass()
        {
            // Arrange
            var command = CreateValidCommand() with { InitialStock = 100 };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        #endregion

        #region Name Validation

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_NameIsEmpty_ShouldFail(string? name)
        {
            // Arrange
            var command = CreateValidCommand() with { Name = name! };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.PropertyName == "Name" &&
                e.ErrorMessage.Contains("required"));
        }

        [Fact]
        public void Validate_NameExceeds200Characters_ShouldFail()
        {
            // Arrange
            var command = CreateValidCommand() with { Name = new string('A', 201) };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.PropertyName == "Name" &&
                e.ErrorMessage.Contains("200"));
        }

        [Fact]
        public void Validate_NameAt200Characters_ShouldPass()
        {
            // Arrange
            var command = CreateValidCommand() with { Name = new string('A', 200) };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        #endregion

        #region SKU Validation

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_SkuIsEmpty_ShouldFail(string? sku)
        {
            // Arrange
            var command = CreateValidCommand() with { Sku = sku! };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.PropertyName == "Sku" &&
                e.ErrorMessage.Contains("required"));
        }

        [Fact]
        public void Validate_SkuExceeds50Characters_ShouldFail()
        {
            // Arrange
            var command = CreateValidCommand() with { Sku = new string('A', 51) };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.PropertyName == "Sku" &&
                e.ErrorMessage.Contains("50"));
        }

        [Theory]
        [InlineData("TEST-001")]
        [InlineData("PROD_123")]
        [InlineData("ABC123")]
        [InlineData("test-product-001")]
        public void Validate_SkuValidFormat_ShouldPass(string sku)
        {
            // Arrange
            var command = CreateValidCommand() with { Sku = sku };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("TEST 001")]
        [InlineData("TEST@001")]
        [InlineData("TEST#001")]
        [InlineData("TEST!001")]
        public void Validate_SkuInvalidFormat_ShouldFail(string sku)
        {
            // Arrange
            var command = CreateValidCommand() with { Sku = sku };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.PropertyName == "Sku" &&
                e.ErrorMessage.Contains("letters, numbers, hyphens"));
        }

        #endregion

        #region Price Validation

        [Fact]
        public void Validate_PriceIsZero_ShouldPass()
        {
            // Arrange
            var command = CreateValidCommand() with { Price = 0m };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_PriceIsPositive_ShouldPass()
        {
            // Arrange
            var command = CreateValidCommand() with { Price = 99.99m };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_PriceIsNegative_ShouldFail()
        {
            // Arrange
            var command = CreateValidCommand() with { Price = -1m };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.PropertyName == "Price" &&
                e.ErrorMessage.Contains("non-negative"));
        }

        #endregion

        #region Currency Validation

        [Theory]
        [InlineData("USD")]
        [InlineData("EUR")]
        [InlineData("GBP")]
        [InlineData("CAD")]
        [InlineData("AUD")]
        public void Validate_CurrencyValid_ShouldPass(string currency)
        {
            // Arrange
            var command = CreateValidCommand() with { Currency = currency };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_CurrencyIsEmpty_ShouldFail(string currency)
        {
            // Arrange
            var command = CreateValidCommand() with { Currency = currency };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Currency");
        }

        [Theory]
        [InlineData("XYZ")]
        [InlineData("JPY")]
        [InlineData("CNY")]
        public void Validate_CurrencyNotSupported_ShouldFail(string currency)
        {
            // Arrange
            var command = CreateValidCommand() with { Currency = currency };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.PropertyName == "Currency" &&
                e.ErrorMessage.Contains("must be one of"));
        }

        [Theory]
        [InlineData("US")]
        [InlineData("USDD")]
        public void Validate_CurrencyInvalidLength_ShouldFail(string currency)
        {
            // Arrange
            var command = CreateValidCommand() with { Currency = currency };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.PropertyName == "Currency" &&
                e.ErrorMessage.Contains("3-letter"));
        }

        #endregion

        #region Description Validation

        [Fact]
        public void Validate_DescriptionNull_ShouldPass()
        {
            // Arrange
            var command = CreateValidCommand() with { Description = null };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_DescriptionExceeds2000Characters_ShouldFail()
        {
            // Arrange
            var command = CreateValidCommand() with { Description = new string('A', 2001) };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.PropertyName == "Description" &&
                e.ErrorMessage.Contains("2000"));
        }

        [Fact]
        public void Validate_DescriptionAt2000Characters_ShouldPass()
        {
            // Arrange
            var command = CreateValidCommand() with { Description = new string('A', 2000) };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        #endregion

        #region InitialStock Validation

        [Fact]
        public void Validate_InitialStockZero_ShouldPass()
        {
            // Arrange
            var command = CreateValidCommand() with { InitialStock = 0 };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_InitialStockPositive_ShouldPass()
        {
            // Arrange
            var command = CreateValidCommand() with { InitialStock = 1000 };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_InitialStockNegative_ShouldFail()
        {
            // Arrange
            var command = CreateValidCommand() with { InitialStock = -1 };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.PropertyName == "InitialStock" &&
                e.ErrorMessage.Contains("negative"));
        }

        #endregion

        #region Multiple Invalid Fields

        [Fact]
        public void Validate_MultipleInvalidFields_ShouldReturnAllErrors()
        {
            // Arrange
            var command = new CreateProductCommand(
                Name: "",
                Sku: "INVALID SKU!",
                Price: -100m,
                Currency: "INVALID",
                Description: new string('X', 3000),
                InitialStock: -5);

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().HaveCountGreaterOrEqualTo(5);
            result.Errors.Should().Contain(e => e.PropertyName == "Name");
            result.Errors.Should().Contain(e => e.PropertyName == "Sku");
            result.Errors.Should().Contain(e => e.PropertyName == "Price");
            result.Errors.Should().Contain(e => e.PropertyName == "Currency");
            result.Errors.Should().Contain(e => e.PropertyName == "InitialStock");
        }

        #endregion

        #region Helper Methods

        private static CreateProductCommand CreateValidCommand() => new(
            Name: "Test Product",
            Sku: "TEST-001",
            Price: 99.99m,
            Currency: "USD",
            Description: null,
            InitialStock: 0);

        #endregion
    }
}
