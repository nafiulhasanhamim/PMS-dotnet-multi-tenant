using PMS.Application.Common.DTOs;
using PMS.Application.Common.Stock;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Stock.Queries.GetStock;

public sealed class GetStockQueryHandler
    : IRequestHandler<GetStockQuery, GridResult<StockListItemDto>>
{
    private const int MaxPageSize = 200;

    private readonly IStockQueries _stock;
    private readonly ILogger<GetStockQueryHandler> _logger;

    public GetStockQueryHandler(IStockQueries stock, ILogger<GetStockQueryHandler> logger)
    {
        _stock = stock;
        _logger = logger;
    }

    public async Task<GridResult<StockListItemDto>> Handle(
        GetStockQuery request, CancellationToken cancellationToken)
    {
        // Clamped rather than validated: a hand-edited pageSize is a request to be capped,
        // not an error worth anybody seeing.
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > MaxPageSize ? 25 : request.PageSize;

        var result = await _stock.ListAsync(
            request.Search,
            request.StockStatus,
            request.ExpiryStatus,
            request.ProductType,
            StockPolicy.ExpiringSoonWindowDays,
            page,
            pageSize,
            cancellationToken);

        // Debug: this runs on every page load and every keystroke of the search box. What is
        // worth having when a list shows the wrong things is the filters actually applied.
        _logger.LogDebug(
            "Listed stock for {ResultCount} of {Total} products: search={Search} "
            + "stock={StockStatus} expiry={ExpiryStatus} type={ProductType} "
            + "page={Page}/{TotalPages}",
            result.Data.Count(), result.Total, request.Search ?? "(none)",
            request.StockStatus, request.ExpiryStatus, request.ProductType,
            result.Page, result.TotalPages);

        return result;
    }
}
