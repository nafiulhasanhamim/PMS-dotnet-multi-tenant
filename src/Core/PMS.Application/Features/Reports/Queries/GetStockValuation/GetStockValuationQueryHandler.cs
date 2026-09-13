using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Stock;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Reports.Queries.GetStockValuation;

public sealed class GetStockValuationQueryHandler
    : IRequestHandler<GetStockValuationQuery, Result<StockValuationPageDto>>
{
    private readonly IReportQueries _reports;

    public GetStockValuationQueryHandler(IReportQueries reports) => _reports = reports;

    public async Task<Result<StockValuationPageDto>> Handle(
        GetStockValuationQuery request, CancellationToken cancellationToken)
    {
        var rows = await _reports.GetStockValuationAsync(
            request.ProductType,
            AlertPaging.Page(request.Page), AlertPaging.Size(request.PageSize),
            cancellationToken);

        var summary = await _reports.GetStockValuationSummaryAsync(
            request.ProductType, cancellationToken);

        return Result.Success(new StockValuationPageDto(rows, summary));
    }
}
