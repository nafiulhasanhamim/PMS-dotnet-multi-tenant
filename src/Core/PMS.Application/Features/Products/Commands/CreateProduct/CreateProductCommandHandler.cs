using PMS.Application.Common.DTOs;
using PMS.Application.Common.Products;
using PMS.Application.Common.Units;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Products.Commands.CreateProduct;

public sealed class CreateProductCommandHandler
    : IRequestHandler<CreateProductCommand, Result<ProductDto>>
{
    private readonly IProductQueries _products;
    private readonly IRepository<Product, IApplicationDbContext> _repository;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ILogger<CreateProductCommandHandler> _logger;

    public CreateProductCommandHandler(
        IProductQueries products,
        IRepository<Product, IApplicationDbContext> repository,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ILogger<CreateProductCommandHandler> logger)
    {
        _products = products;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ProductDto>> Handle(
        CreateProductCommand request, CancellationToken cancellationToken)
    {
        // Uniqueness is per pharmacy, and the check needs no tenant argument: the query
        // filter already restricts it to this one. Two pharmacies may both stock Napa 500.
        var clash = await _products.ExistsWithBrandAndStrengthAsync(
            request.BrandName, request.Strength, excludingId: null, cancellationToken);

        if (clash)
        {
            // Logged here rather than left to the pipeline, because the pipeline can only say
            // that a CreateProductCommand was rejected - not which name collided. "Why will it
            // not let me add this?" is answerable from this line alone.
            _logger.LogWarning(
                "Product not created: '{BrandName}' at {Strength} already exists in this pharmacy",
                request.BrandName.Trim(),
                string.IsNullOrWhiteSpace(request.Strength) ? "(no strength)" : request.Strength.Trim());

            return Result.Failure<ProductDto>(Error.Conflict(
                string.IsNullOrWhiteSpace(request.Strength)
                    ? $"You already have a product called '{request.BrandName.Trim()}'."
                    : $"You already have '{request.BrandName.Trim()}' at {request.Strength.Trim()}."));
        }

        var product = new Product(
            request.ProductType, request.BrandName, request.BaseUnitName, request.PricePerBase);

        // TenantId is not set here and must not be: the persistence interceptor stamps it on
        // insert. A handler that set it could write into another pharmacy's data.
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

        if (request.CatalogMedicineId is not null)
        {
            product.LinkToCatalog(request.CatalogMedicineId.Value);
        }

        await _repository.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // The identity of the row, which is the one thing the pipeline behaviour cannot
        // report: it sees "CreateProductCommand completed", not which product now exists.
        // Whether it came from the catalogue is worth recording too - an imported product and
        // a hand-typed one are investigated differently when the details turn out wrong.
        _logger.LogInformation(
            "Product created {ProductId} '{BrandName}' type={ProductType} "
            + "units={BaseUnit}/{MidUnit}/{LargeUnit} antibiotic={IsAntibiotic} source={Source}",
            product.Id,
            product.BrandName,
            product.ProductType,
            product.BaseUnitName,
            product.MidUnitName ?? "-",
            product.LargeUnitName ?? "-",
            product.IsAntibiotic,
            product.CatalogMedicineId is { } catalogId ? $"catalog:{catalogId}" : "manual");

        return ProductMapping.ToDto(product);
    }
}
