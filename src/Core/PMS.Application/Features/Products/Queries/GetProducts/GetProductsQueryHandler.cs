using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Products.Queries.GetProducts;

public sealed class GetProductsQueryHandler
    : IRequestHandler<GetProductsQuery, GridResult<ProductListItemDto>>
{
    private const int MaxPageSize = 200;

    private readonly IProductQueries _products;
    private readonly ILogger<GetProductsQueryHandler> _logger;

    public GetProductsQueryHandler(
        IProductQueries products,
        ILogger<GetProductsQueryHandler> logger)
    {
        _products = products;
        _logger = logger;
    }

    public async Task<GridResult<ProductListItemDto>> Handle(
        GetProductsQuery request, CancellationToken cancellationToken)
    {
        // Clamped here rather than validated: a hand-edited pageSize of 100000 is a request
        // to be capped, not an error worth showing anybody.
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > MaxPageSize ? 25 : request.PageSize;

        var result = await _products.ListAsync(
            request.ListType,
            request.Search,
            request.Status,
            request.AntibioticOnly,
            request.ProductType,
            request.IncludePrices,
            page,
            pageSize,
            cancellationToken);

        // Debug, deliberately. This runs on every page load and every keystroke of a search,
        // so at Information it would be the single noisiest line in the file - and the
        // pipeline behaviour already records that a GetProductsQuery ran. What is here is the
        // detail you would want when a list "shows the wrong things": the filters actually
        // applied, and whether prices were withheld for this caller's role.
        _logger.LogDebug(
            "Listed {ResultCount} of {Total} products: list={ListType} search='{Search}' "
            + "status={Status} antibioticOnly={AntibioticOnly} type={ProductType} "
            + "page={Page}/{TotalPages} prices={PricesIncluded}",
            result.Data.Count(),
            result.Total,
            request.ListType,
            request.Search ?? string.Empty,
            request.Status,
            request.AntibioticOnly,
            request.ProductType?.ToString() ?? "any",
            result.Page,
            result.TotalPages,
            request.IncludePrices);

        return result;
    }
}
