using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Stock.Queries.GetBatchAdjustments;

public sealed class GetBatchAdjustmentsQueryHandler
    : IRequestHandler<GetBatchAdjustmentsQuery, GridResult<StockAdjustmentDto>>
{
    private const int MaxPageSize = 200;

    private readonly IStockQueries _stock;
    private readonly ILogger<GetBatchAdjustmentsQueryHandler> _logger;

    public GetBatchAdjustmentsQueryHandler(
        IStockQueries stock, ILogger<GetBatchAdjustmentsQueryHandler> logger)
    {
        _stock = stock;
        _logger = logger;
    }

    public async Task<GridResult<StockAdjustmentDto>> Handle(
        GetBatchAdjustmentsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > MaxPageSize ? 20 : request.PageSize;

        var result = await _stock.ListAdjustmentsAsync(
            request.BatchId, page, pageSize, cancellationToken);

        _logger.LogDebug(
            "Listed {Count} of {Total} adjustments for batch {BatchId}",
            result.Data.Count(), result.Total, request.BatchId);

        return result;
    }
}
