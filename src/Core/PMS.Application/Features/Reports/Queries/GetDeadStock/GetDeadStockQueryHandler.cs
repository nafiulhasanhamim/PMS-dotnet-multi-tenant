using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Stock;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Reports.Queries.GetDeadStock;

public sealed class GetDeadStockQueryHandler
    : IRequestHandler<GetDeadStockQuery, Result<DeadStockPageDto>>
{
    private readonly IReportQueries _reports;

    public GetDeadStockQueryHandler(IReportQueries reports) => _reports = reports;

    public async Task<Result<DeadStockPageDto>> Handle(
        GetDeadStockQuery request, CancellationToken cancellationToken)
    {
        // Coerced once, here, and both calls below get the same number. Coercing separately in
        // each would let a hand-crafted threshold produce a summary over a different window than
        // the rows it heads.
        var threshold = StockPolicy.CoerceDeadStockThreshold(request.ThresholdDays);

        var rows = await _reports.GetDeadStockAsync(
            threshold, request.ProductType,
            AlertPaging.Page(request.Page), AlertPaging.Size(request.PageSize),
            cancellationToken);

        var summary = await _reports.GetDeadStockSummaryAsync(
            threshold, request.ProductType, cancellationToken);

        return Result.Success(new DeadStockPageDto(threshold, rows, summary));
    }
}
