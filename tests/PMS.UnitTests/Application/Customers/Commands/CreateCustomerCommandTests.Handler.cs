using PMS.Application.Common.DTOs;
using PMS.Application.Features.Customers.Commands.CreateCustomer;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.UnitTests.Common;
using FluentAssertions;
using Moq;
using Xunit;

namespace PMS.UnitTests.Application.Customers.Commands;

public partial class CreateCustomerCommandTests
{
    public class HandlerTests
    {
        private readonly Mock<IRepository<Customer, IApplicationDbContext>> _customerRepository;
        private readonly Mock<IUnitOfWork<IApplicationDbContext>> _unitOfWork;
        private readonly CreateCustomerCommandHandler _handler;

        static HandlerTests()
        {
            MapsterTestFixture.Initialize();
        }

        public HandlerTests()
        {
            _customerRepository = new Mock<IRepository<Customer, IApplicationDbContext>>();
            _unitOfWork = new Mock<IUnitOfWork<IApplicationDbContext>>();

            _handler = new CreateCustomerCommandHandler(
                _customerRepository.Object,
                _unitOfWork.Object);
        }

        #region Success Scenarios

        [Fact]
        public async Task Handle_ValidCommand_CreatesCustomerAndReturnsDto()
        {
            // Arrange
            var command = CreateValidCommand();

            _customerRepository.Setup(x => x.FirstOrDefaultAsync(
                    It.IsAny<Ardalis.Specification.ISpecification<Customer>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Customer?)null);

            Customer? capturedCustomer = null;
            _customerRepository.Setup(x => x.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()))
                .Callback<Customer, CancellationToken>((c, _) => capturedCustomer = c)
                .ReturnsAsync((Customer c, CancellationToken _) => c);

            _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.FirstName.Should().Be(command.FirstName);
            result.Value.LastName.Should().Be(command.LastName);
            result.Value.Email.Should().Be(command.Email);

            capturedCustomer.Should().NotBeNull();
            capturedCustomer!.FirstName.Should().Be(command.FirstName);
            capturedCustomer.LastName.Should().Be(command.LastName);
        }

        [Fact]
        public async Task Handle_ValidCommandWithPhoneNumber_SetsPhoneNumber()
        {
            // Arrange
            var command = CreateValidCommand() with { PhoneNumber = "+1-555-123-4567" };

            _customerRepository.Setup(x => x.FirstOrDefaultAsync(
                    It.IsAny<Ardalis.Specification.ISpecification<Customer>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Customer?)null);

            Customer? capturedCustomer = null;
            _customerRepository.Setup(x => x.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()))
                .Callback<Customer, CancellationToken>((c, _) => capturedCustomer = c)
                .ReturnsAsync((Customer c, CancellationToken _) => c);

            _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            capturedCustomer.Should().NotBeNull();
            capturedCustomer!.PhoneNumber.Should().NotBeNull();
        }

        [Fact]
        public async Task Handle_ValidCommand_CallsRepositoryAddOnce()
        {
            // Arrange
            var command = CreateValidCommand();

            _customerRepository.Setup(x => x.FirstOrDefaultAsync(
                    It.IsAny<Ardalis.Specification.ISpecification<Customer>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Customer?)null);

            _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            _customerRepository.Verify(
                x => x.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_ValidCommand_SavesChangesAtomically()
        {
            // Arrange
            var command = CreateValidCommand();

            _customerRepository.Setup(x => x.FirstOrDefaultAsync(
                    It.IsAny<Ardalis.Specification.ISpecification<Customer>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Customer?)null);

            _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            _unitOfWork.Verify(
                x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Error Scenarios

        [Fact]
        public async Task Handle_DuplicateEmail_ReturnsConflictError()
        {
            // Arrange
            var command = CreateValidCommand();
            var existingCustomer = CreateTestCustomer();

            _customerRepository.Setup(x => x.FirstOrDefaultAsync(
                    It.IsAny<Ardalis.Specification.ISpecification<Customer>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingCustomer);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            result.Error.Description.Should().Contain("already exists");
        }

        [Fact]
        public async Task Handle_DuplicateEmail_DoesNotAddCustomer()
        {
            // Arrange
            var command = CreateValidCommand();
            var existingCustomer = CreateTestCustomer();

            _customerRepository.Setup(x => x.FirstOrDefaultAsync(
                    It.IsAny<Ardalis.Specification.ISpecification<Customer>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingCustomer);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            _customerRepository.Verify(
                x => x.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task Handle_InvalidEmail_ReturnsFailure()
        {
            // Arrange
            var command = CreateValidCommand() with { Email = "invalid-email" };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
        }

        #endregion

        #region Helper Methods

        private static CreateCustomerCommand CreateValidCommand() => new(
            FirstName: "John",
            LastName: "Doe",
            Email: "john.doe@example.com",
            PhoneNumber: null);

        private static Customer CreateTestCustomer()
        {
            return new Customer("Existing", "Customer",
                PMS.Domain.ValueObjects.Email.Create("existing@example.com").Value);
        }

        #endregion
    }
}
