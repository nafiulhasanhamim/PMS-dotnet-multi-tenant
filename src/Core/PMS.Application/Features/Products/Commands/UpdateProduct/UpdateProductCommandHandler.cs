using PMS.Application.Common.DTOs;
using PMS.Application.Common.Products;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Products.Commands.UpdateProduct;

public sealed class UpdateProductCommandHandler
    : IRequestHandler<UpdateProductCommand, Result<ProductDto>>
{
    private readonly IProductQueries _products;
    private readonly IRepository<Product, IApplicationDbContext> _repository;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ILogger<UpdateProductCommandHandler> _logger;

    public UpdateProductCommandHandler(
        IProductQueries products,
        IRepository<Product, IApplicationDbContext> repository,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ILogger<UpdateProductCommandHandler> logger)
    {
        _products = products;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ProductDto>> Handle(
        UpdateProductCommand request, CancellationToken cancellationToken)
    {
        // Filtered, so another pharmacy's product is absent rather than forbidden: the caller
        // gets a 404 and learns nothing about whether that id exists elsewhere.
        var product = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (product is null)
        {
            // Worth a line: behind the query filter this is indistinguishable from another
            // pharmacy's id, so a burst of these is either a stale bookmark or someone
            // probing ids that are not theirs.
            _logger.LogWarning(
                "Product not updated: {ProductId} not found in this pharmacy", request.Id);

            return Result.Failure<ProductDto>(Error.NotFound("Product", request.Id));
        }

        var clash = await _products.ExistsWithIdentityAsync(
            request.BrandName, request.Strength, request.DosageForm,
            excludingId: request.Id, cancellationToken: cancellationToken);

        if (clash)
        {
            var identity = ProductKeys.Describe(
                request.BrandName, request.Strength, request.DosageForm);

            _logger.LogWarning(
                "Product {ProductId} not updated: {Identity} would collide with another "
                + "product in this pharmacy",
                request.Id, identity);

            return Result.Failure<ProductDto>(Error.Conflict(
                $"You already have another product that is {identity}."));
        }

        product.Update(
            request.ProductType,
            request.BrandName,
            request.Company,
            request.Category,
            request.GenericName,
            request.Strength,
            request.DosageForm,
            request.IsAntibiotic,
            request.BaseUnitName,
            request.MidUnitName,
            request.LargeUnitName,
            request.BasePerMid,
            request.MidPerLarge,
            request.PricePerBase,
            request.PricePerMid,
            request.PricePerLarge,
            request.ReorderLevel,
            request.ShelfLocation);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Prices and the antibiotic flag are called out specifically: both change what
        // happens at the till, so "when did this price change, and to what?" is a question
        // the log should answer without a database audit query.
        _logger.LogInformation(
            "Product updated {ProductId} '{BrandName}' units={BaseUnit}/{MidUnit}/{LargeUnit} "
            + "price={PricePerBase} antibiotic={IsAntibiotic} active={IsActive}",
            product.Id,
            product.BrandName,
            product.BaseUnitName,
            product.MidUnitName ?? "-",
            product.LargeUnitName ?? "-",
            product.PricePerBase,
            product.IsAntibiotic,
            product.IsActive);

        return ProductMapping.ToDto(product);
    }
}
