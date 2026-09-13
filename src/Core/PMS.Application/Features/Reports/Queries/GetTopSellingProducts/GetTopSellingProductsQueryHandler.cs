using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Reports;
using PMS.Application.Common.Stock;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Reports.Queries.GetTopSellingProducts;

public sealed class GetTopSellingProductsQueryHandler
    : IRequestHandler<GetTopSellingProductsQuery, Result<GridResult<TopSellingProductDto>>>
{
    private readonly IReportQueries _reports;
    private readonly IDateTime _clock;

    public GetTopSellingProductsQueryHandler(IReportQueries reports, IDateTime clock)
    {
        _reports = reports;
        _clock = clock;
    }

    public async Task<Result<GridResult<TopSellingProductDto>>> Handle(
        GetTopSellingProductsQuery request, CancellationToken cancellationToken)
    {
        var (from, to) = ReportRange.Resolve(request.From, request.To, _clock.UtcDateToday());

        return Result.Success(await _reports.GetTopSellingProductsAsync(
            from, to, request.SortBy, request.ProductType,
            AlertPaging.Page(request.Page), AlertPaging.Size(request.PageSize),
            cancellationToken));
    }
}
