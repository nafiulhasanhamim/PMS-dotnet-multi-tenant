using PMS.Application.Common.DTOs;
using PMS.Application.Common.Stock;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Stock.Queries.GetBatch;

public sealed class GetBatchQueryHandler : IRequestHandler<GetBatchQuery, Result<BatchDto>>
{
    private readonly IStockQueries _stock;
    private readonly ILogger<GetBatchQueryHandler> _logger;

    public GetBatchQueryHandler(IStockQueries stock, ILogger<GetBatchQueryHandler> logger)
    {
        _stock = stock;
        _logger = logger;
    }

    public async Task<Result<BatchDto>> Handle(
        GetBatchQuery request, CancellationToken cancellationToken)
    {
        var batch = await _stock.FindBatchAsync(
            request.BatchId,
            request.IncludePurchasePrices,
            StockPolicy.ExpiringSoonWindowDays,
            cancellationToken);

        if (batch is null)
        {
            _logger.LogWarning("Batch {BatchId} not found in this pharmacy", request.BatchId);

            return Result.Failure<BatchDto>(Error.NotFound(nameof(Batch), request.BatchId));
        }

        return batch;
    }
}
