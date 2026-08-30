using Ardalis.GuardClauses;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Mappings;
using PMS.Application.Features.Products.Specifications;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Products.Queries.GetProductById;

/// <summary>
/// Handler for GetProductByIdQuery.
/// </summary>
public sealed class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, Result<ProductDto>>
{
    private readonly IReadRepository<Product, IApplicationDbContext> _productRepository;

    public GetProductByIdQueryHandler(IReadRepository<Product, IApplicationDbContext> productRepository)
    {
        _productRepository = Guard.Against.Null(productRepository);
    }

    public async Task<Result<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.FirstOrDefaultAsync(
            new ProductByIdSpec(request.Id), cancellationToken);

        if (product is null)
        {
            return Result.Failure<ProductDto>(
                Error.NotFound("Product", request.Id));
        }

        return product.ToDto();
    }
}
