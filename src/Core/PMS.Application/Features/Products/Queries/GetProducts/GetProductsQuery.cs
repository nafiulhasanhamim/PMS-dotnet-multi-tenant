using PMS.Application.Common.DTOs;
using MediatR;

namespace PMS.Application.Features.Products.Queries.GetProducts;

/// <summary>
/// Query to get a list of products with optional filtering and pagination.
/// </summary>
public sealed record GetProductsQuery(
    string? SearchTerm = null,
    bool? IsActive = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    int Page = 1,
    int PageSize = 10) : IRequest<GetProductsResponse>;

/// <summary>
/// Response for GetProductsQuery.
/// </summary>
public sealed record GetProductsResponse(
    List<ProductDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
