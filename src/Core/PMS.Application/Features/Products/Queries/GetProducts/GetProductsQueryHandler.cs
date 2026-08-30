using Ardalis.GuardClauses;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Mappings;
using PMS.Application.Features.Products.Specifications;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using MediatR;

namespace PMS.Application.Features.Products.Queries.GetProducts;

/// <summary>
/// Handler for GetProductsQuery.
/// </summary>
public sealed class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, GetProductsResponse>
{
    private readonly IReadRepository<Product, IApplicationDbContext> _productRepository;

    public GetProductsQueryHandler(IReadRepository<Product, IApplicationDbContext> productRepository)
    {
        _productRepository = Guard.Against.Null(productRepository);
    }

    public async Task<GetProductsResponse> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        // Get total count
        var countSpec = new ProductsListSpec(
            request.SearchTerm,
            request.IsActive,
            request.MinPrice,
            request.MaxPrice);
        var totalCount = await _productRepository.CountAsync(countSpec, cancellationToken);

        // Get paginated results
        var skip = (request.Page - 1) * request.PageSize;
        var spec = new ProductsListSpec(
            request.SearchTerm,
            request.IsActive,
            request.MinPrice,
            request.MaxPrice,
            skip,
            request.PageSize);
        var products = await _productRepository.ListAsync(spec, cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new GetProductsResponse(
            products.Select(p => p.ToDto()).ToList(),
            totalCount,
            request.Page,
            request.PageSize,
            totalPages);
    }
}
