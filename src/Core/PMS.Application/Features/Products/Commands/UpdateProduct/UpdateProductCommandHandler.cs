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

namespace PMS.Application.Features.Products.Commands.UpdateProduct;

/// <summary>
/// Handler for UpdateProductCommand.
/// </summary>
public sealed class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Result<ProductDto>>
{
    private readonly IRepository<Product, IApplicationDbContext> _productRepository;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;

    public UpdateProductCommandHandler(
        IRepository<Product, IApplicationDbContext> productRepository,
        IUnitOfWork<IApplicationDbContext> unitOfWork)
    {
        _productRepository = Guard.Against.Null(productRepository);
        _unitOfWork = Guard.Against.Null(unitOfWork);
    }

    public async Task<Result<ProductDto>> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        // Get product by ID
        var product = await _productRepository.FirstOrDefaultAsync(
            new ProductByIdSpec(request.Id), cancellationToken);

        if (product is null)
        {
            return Result.Failure<ProductDto>(
                Error.NotFound("Product", request.Id));
        }

        // Check for duplicate SKU (if SKU is being changed)
        if (!string.Equals(product.Sku, request.Sku, StringComparison.OrdinalIgnoreCase))
        {
            var existingProduct = await _productRepository.FirstOrDefaultAsync(
                new ProductBySkuSpec(request.Sku), cancellationToken);

            if (existingProduct is not null)
            {
                return Result.Failure<ProductDto>(
                    Error.Conflict($"Product with SKU '{request.Sku}' already exists."));
            }
        }

        // Create Money value object
        var priceResult = Money.Create(request.Price, request.Currency);
        if (priceResult.IsFailure)
        {
            return Result.Failure<ProductDto>(priceResult.Error);
        }

        // Update product
        product.UpdateDetails(request.Name, request.Sku, priceResult.Value, request.Description);

        await _productRepository.UpdateAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return product.ToDto();
    }
}
