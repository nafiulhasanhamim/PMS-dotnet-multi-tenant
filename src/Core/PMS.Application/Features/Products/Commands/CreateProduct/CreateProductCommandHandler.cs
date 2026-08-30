using Ardalis.GuardClauses;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Mappings;
using PMS.Application.Features.Products.Specifications;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.ValueObjects;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Products.Commands.CreateProduct;

/// <summary>
/// Handler for CreateProductCommand.
/// </summary>
public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<ProductDto>>
{
    private readonly IRepository<Product, IApplicationDbContext> _productRepository;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;

    public CreateProductCommandHandler(
        IRepository<Product, IApplicationDbContext> productRepository,
        IUnitOfWork<IApplicationDbContext> unitOfWork)
    {
        _productRepository = Guard.Against.Null(productRepository);
        _unitOfWork = Guard.Against.Null(unitOfWork);
    }

    public async Task<Result<ProductDto>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        // Check for duplicate SKU
        var existingProduct = await _productRepository.FirstOrDefaultAsync(
            new ProductBySkuSpec(request.Sku), cancellationToken);

        if (existingProduct is not null)
        {
            return Result.Failure<ProductDto>(
                Error.Conflict($"Product with SKU '{request.Sku}' already exists."));
        }

        // Create Money value object
        var priceResult = Money.Create(request.Price, request.Currency);
        if (priceResult.IsFailure)
        {
            return Result.Failure<ProductDto>(priceResult.Error);
        }

        // Create product
        var product = new Product(request.Name, request.Sku, priceResult.Value, request.Description);

        // Add initial stock if provided
        if (request.InitialStock > 0)
        {
            product.AddStock(request.InitialStock);
        }

        await _productRepository.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return product.ToDto();
    }
}
