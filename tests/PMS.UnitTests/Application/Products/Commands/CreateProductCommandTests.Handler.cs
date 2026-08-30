using PMS.Application.Features.Products.Commands.CreateProduct;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.ValueObjects;
using PMS.SharedKernel.Interfaces;
using PMS.UnitTests.Common;
using FluentAssertions;
using Moq;
using Xunit;

namespace PMS.UnitTests.Application.Products.Commands;

public partial class CreateProductCommandTests
{
    public class HandlerTests
    {
        private readonly Mock<IRepository<Product, IApplicationDbContext>> _productRepository;
        private readonly Mock<IUnitOfWork<IApplicationDbContext>> _unitOfWork;
        private readonly CreateProductCommandHandler _handler;

        static HandlerTests()
        {
            MapsterTestFixture.Initialize();
        }

        public HandlerTests()
        {
            _productRepository = new Mock<IRepository<Product, IApplicationDbContext>>();
            _unitOfWork = new Mock<IUnitOfWork<IApplicationDbContext>>();

            _handler = new CreateProductCommandHandler(
                _productRepository.Object,
                _unitOfWork.Object);
        }

        #region Success Scenarios

        [Fact]
        public async Task Handle_ValidCommand_CreatesProductAndReturnsDto()
        {
            // Arrange
            var command = CreateValidCommand();

            _productRepository.Setup(x => x.FirstOrDefaultAsync(
                    It.IsAny<Ardalis.Specification.ISpecification<Product>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Product?)null);

            Product? capturedProduct = null;
            _productRepository.Setup(x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
                .Callback<Product, CancellationToken>((p, _) => capturedProduct = p)
                .ReturnsAsync((Product p, CancellationToken _) => p);

            _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Name.Should().Be(command.Name);
            result.Value.Sku.Should().Be(command.Sku);

            capturedProduct.Should().NotBeNull();
            capturedProduct!.Name.Should().Be(command.Name);
            capturedProduct.Sku.Should().Be(command.Sku);
        }

        [Fact]
        public async Task Handle_ValidCommandWithDescription_SetsDescription()
        {
            // Arrange
            var command = CreateValidCommand() with { Description = "Test product description" };

            _productRepository.Setup(x => x.FirstOrDefaultAsync(
                    It.IsAny<Ardalis.Specification.ISpecification<Product>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Product?)null);

            Product? capturedProduct = null;
            _productRepository.Setup(x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
                .Callback<Product, CancellationToken>((p, _) => capturedProduct = p)
                .ReturnsAsync((Product p, CancellationToken _) => p);

            _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            capturedProduct.Should().NotBeNull();
            capturedProduct!.Description.Should().Be(command.Description);
        }

        [Fact]
        public async Task Handle_ValidCommandWithInitialStock_AddsStock()
        {
            // Arrange
            var command = CreateValidCommand() with { InitialStock = 100 };

            _productRepository.Setup(x => x.FirstOrDefaultAsync(
                    It.IsAny<Ardalis.Specification.ISpecification<Product>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Product?)null);

            Product? capturedProduct = null;
            _productRepository.Setup(x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
                .Callback<Product, CancellationToken>((p, _) => capturedProduct = p)
                .ReturnsAsync((Product p, CancellationToken _) => p);

            _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            capturedProduct.Should().NotBeNull();
            capturedProduct!.StockQuantity.Should().Be(100);
        }

        [Fact]
        public async Task Handle_ValidCommandWithZeroInitialStock_DoesNotAddStock()
        {
            // Arrange
            var command = CreateValidCommand() with { InitialStock = 0 };

            _productRepository.Setup(x => x.FirstOrDefaultAsync(
                    It.IsAny<Ardalis.Specification.ISpecification<Product>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Product?)null);

            Product? capturedProduct = null;
            _productRepository.Setup(x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
                .Callback<Product, CancellationToken>((p, _) => capturedProduct = p)
                .ReturnsAsync((Product p, CancellationToken _) => p);

            _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            capturedProduct.Should().NotBeNull();
            capturedProduct!.StockQuantity.Should().Be(0);
        }

        [Fact]
        public async Task Handle_ValidCommand_CallsRepositoryAddOnce()
        {
            // Arrange
            var command = CreateValidCommand();

            _productRepository.Setup(x => x.FirstOrDefaultAsync(
                    It.IsAny<Ardalis.Specification.ISpecification<Product>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Product?)null);

            _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            _productRepository.Verify(
                x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_ValidCommand_SavesChangesAtomically()
        {
            // Arrange
            var command = CreateValidCommand();

            _productRepository.Setup(x => x.FirstOrDefaultAsync(
                    It.IsAny<Ardalis.Specification.ISpecification<Product>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Product?)null);

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
        public async Task Handle_DuplicateSku_ReturnsConflictError()
        {
            // Arrange
            var command = CreateValidCommand();
            var existingProduct = CreateTestProduct();

            _productRepository.Setup(x => x.FirstOrDefaultAsync(
                    It.IsAny<Ardalis.Specification.ISpecification<Product>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingProduct);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            result.Error.Description.Should().Contain("already exists");
        }

        [Fact]
        public async Task Handle_DuplicateSku_DoesNotAddProduct()
        {
            // Arrange
            var command = CreateValidCommand();
            var existingProduct = CreateTestProduct();

            _productRepository.Setup(x => x.FirstOrDefaultAsync(
                    It.IsAny<Ardalis.Specification.ISpecification<Product>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingProduct);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            _productRepository.Verify(
                x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task Handle_NegativePrice_ReturnsFailure()
        {
            // Arrange
            var command = CreateValidCommand() with { Price = -10.00m };

            _productRepository.Setup(x => x.FirstOrDefaultAsync(
                    It.IsAny<Ardalis.Specification.ISpecification<Product>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Product?)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
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

        private static Product CreateTestProduct()
        {
            var price = Money.Create(49.99m, "USD").Value;
            return new Product("Existing Product", "EXIST-001", price, "Existing description");
        }

        #endregion
    }
}
