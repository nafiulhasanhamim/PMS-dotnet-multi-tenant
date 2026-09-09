using PMS.Application.Common.DTOs;
using PMS.Application.Common.Stock;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Stock.Queries.GetProductStock;

public sealed class GetProductStockQueryHandler
    : IRequestHandler<GetProductStockQuery, Result<ProductStockDto>>
{
    private readonly IStockQueries _stock;
    private readonly ILogger<GetProductStockQueryHandler> _logger;

    public GetProductStockQueryHandler(
        IStockQueries stock, ILogger<GetProductStockQueryHandler> logger)
    {
        _stock = stock;
        _logger = logger;
    }

    public async Task<Result<ProductStockDto>> Handle(
        GetProductStockQuery request, CancellationToken cancellationToken)
    {
        var page = request.DepletedPage < 1 ? 1 : request.DepletedPage;
        var pageSize = request.DepletedPageSize is < 1 or > 100 ? 10 : request.DepletedPageSize;

        var stock = await _stock.GetProductStockAsync(
            request.ProductId,
            request.IncludePurchasePrices,
            StockPolicy.ExpiringSoonWindowDays,
            page,
            pageSize,
            cancellationToken);

        if (stock is null)
        {
            _logger.LogWarning(
                "Stock for product {ProductId} not found in this pharmacy", request.ProductId);

            return Result.Failure<ProductStockDto>(
                Error.NotFound(nameof(Product), request.ProductId));
        }

        _logger.LogDebug(
            "Stock for {BrandName}: {Total} base units across {BatchCount} batches, "
            + "{ActiveCount} active, {DepletedCount} depleted, prices={PricesIncluded}",
            stock.BrandName, stock.TotalQuantityInBaseUnits, stock.BatchCount,
            stock.ActiveBatches.Count, stock.DepletedBatchCount, request.IncludePurchasePrices);

        return stock;
    }
}
