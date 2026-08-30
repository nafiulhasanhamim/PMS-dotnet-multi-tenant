using PMS.Application.Features.Customers.Commands.CreateCustomer;
using FluentAssertions;
using Xunit;

namespace PMS.UnitTests.Application.Customers.Commands;

public partial class CreateCustomerCommandTests
{
    public class ValidatorTests
    {
        private readonly CreateCustomerCommandValidator _validator;

        public ValidatorTests()
        {
            _validator = new CreateCustomerCommandValidator();
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
        public void Validate_ValidCommandWithPhoneNumber_ShouldPass()
        {
            // Arrange
            var command = CreateValidCommand() with { PhoneNumber = "+1-555-123-4567" };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_ValidCommandWithNullPhoneNumber_ShouldPass()
        {
            // Arrange
            var command = CreateValidCommand() with { PhoneNumber = null };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        #endregion

        #region FirstName Validation

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_FirstNameIsEmpty_ShouldFail(string? firstName)
        {
            // Arrange
            var command = CreateValidCommand() with { FirstName = firstName! };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.PropertyName == "FirstName" &&
                e.ErrorMessage.Contains("required"));
        }

        [Fact]
        public void Validate_FirstNameExceeds100Characters_ShouldFail()
        {
            // Arrange
            var command = CreateValidCommand() with { FirstName = new string('A', 101) };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.PropertyName == "FirstName" &&
                e.ErrorMessage.Contains("100"));
        }

        [Fact]
        public void Validate_FirstNameAt100Characters_ShouldPass()
        {
            // Arrange
            var command = CreateValidCommand() with { FirstName = new string('A', 100) };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        #endregion

        #region LastName Validation

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_LastNameIsEmpty_ShouldFail(string? lastName)
        {
            // Arrange
            var command = CreateValidCommand() with { LastName = lastName! };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.PropertyName == "LastName" &&
                e.ErrorMessage.Contains("required"));
        }

        [Fact]
        public void Validate_LastNameExceeds100Characters_ShouldFail()
        {
            // Arrange
            var command = CreateValidCommand() with { LastName = new string('B', 101) };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.PropertyName == "LastName" &&
                e.ErrorMessage.Contains("100"));
        }

        #endregion

        #region Email Validation

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_EmailIsEmpty_ShouldFail(string? email)
        {
            // Arrange
            var command = CreateValidCommand() with { Email = email! };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.PropertyName == "Email" &&
                e.ErrorMessage.Contains("required"));
        }

        [Theory]
        [InlineData("invalid")]
        [InlineData("invalid@")]
        [InlineData("@invalid.com")]
        [InlineData("invalid.com")]
        public void Validate_EmailInvalidFormat_ShouldFail(string email)
        {
            // Arrange
            var command = CreateValidCommand() with { Email = email };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.PropertyName == "Email" &&
                e.ErrorMessage.Contains("Invalid email"));
        }

        [Theory]
        [InlineData("test@example.com")]
        [InlineData("user.name@domain.org")]
        [InlineData("user+tag@example.co.uk")]
        public void Validate_EmailValidFormat_ShouldPass(string email)
        {
            // Arrange
            var command = CreateValidCommand() with { Email = email };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Validate_EmailExceeds256Characters_ShouldFail()
        {
            // Arrange - 251 + 6 (@b.com) = 257 characters, exceeding the 256 limit
            var longEmail = new string('a', 251) + "@b.com";
            var command = CreateValidCommand() with { Email = longEmail };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.PropertyName == "Email" &&
                e.ErrorMessage.Contains("256"));
        }

        #endregion

        #region PhoneNumber Validation

        [Theory]
        [InlineData("+1-555-123-4567")]
        [InlineData("555-123-4567")]
        [InlineData("(555) 123-4567")]
        [InlineData("+44 20 7946 0958")]
        public void Validate_PhoneNumberValidFormat_ShouldPass(string phoneNumber)
        {
            // Arrange
            var command = CreateValidCommand() with { PhoneNumber = phoneNumber };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("abc123")]
        [InlineData("phone!@#$")]
        public void Validate_PhoneNumberInvalidFormat_ShouldFail(string phoneNumber)
        {
            // Arrange
            var command = CreateValidCommand() with { PhoneNumber = phoneNumber };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.PropertyName == "PhoneNumber" &&
                e.ErrorMessage.Contains("Invalid phone"));
        }

        [Fact]
        public void Validate_PhoneNumberExceeds20Characters_ShouldFail()
        {
            // Arrange
            var command = CreateValidCommand() with { PhoneNumber = new string('1', 21) };

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e =>
                e.PropertyName == "PhoneNumber" &&
                e.ErrorMessage.Contains("20"));
        }

        #endregion

        #region Multiple Invalid Fields

        [Fact]
        public void Validate_MultipleInvalidFields_ShouldReturnAllErrors()
        {
            // Arrange
            var command = new CreateCustomerCommand(
                FirstName: "",
                LastName: "",
                Email: "invalid",
                PhoneNumber: "abc");

            // Act
            var result = _validator.Validate(command);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().HaveCountGreaterOrEqualTo(4);
            result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
            result.Errors.Should().Contain(e => e.PropertyName == "LastName");
            result.Errors.Should().Contain(e => e.PropertyName == "Email");
            result.Errors.Should().Contain(e => e.PropertyName == "PhoneNumber");
        }

        #endregion

        #region Helper Methods

        private static CreateCustomerCommand CreateValidCommand() => new(
            FirstName: "John",
            LastName: "Doe",
            Email: "john.doe@example.com",
            PhoneNumber: null);

        #endregion
    }
}
