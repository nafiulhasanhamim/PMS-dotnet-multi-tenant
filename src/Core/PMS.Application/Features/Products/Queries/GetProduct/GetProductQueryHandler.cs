using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Products.Queries.GetProduct;

public sealed class GetProductQueryHandler : IRequestHandler<GetProductQuery, Result<ProductDto>>
{
    private readonly IProductQueries _products;
    private readonly ILogger<GetProductQueryHandler> _logger;

    public GetProductQueryHandler(
        IProductQueries products,
        ILogger<GetProductQueryHandler> logger)
    {
        _products = products;
        _logger = logger;
    }

    public async Task<Result<ProductDto>> Handle(
        GetProductQuery request, CancellationToken cancellationToken)
    {
        var product = await _products.FindAsync(request.Id, cancellationToken);

        // Another pharmacy's product is genuinely absent here, not forbidden - the query
        // filter removed it before this code ran. A 404 is therefore the honest answer, and
        // it also tells the caller nothing about whether that id exists elsewhere.
        if (product is null)
        {
            // Behind the query filter, "another pharmacy's product" and "no such product"
            // look identical. Logging it is how a pattern of the former becomes visible.
            _logger.LogWarning(
                "Product {ProductId} not found in this pharmacy", request.Id);

            return Result.Failure<ProductDto>(Error.NotFound("Product", request.Id));
        }

        return product;
    }
}
