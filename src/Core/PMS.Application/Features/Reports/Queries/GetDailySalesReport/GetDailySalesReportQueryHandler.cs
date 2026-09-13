using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Reports.Queries.GetDailySalesReport;

public sealed class GetDailySalesReportQueryHandler
    : IRequestHandler<GetDailySalesReportQuery, Result<DailySalesReportDto>>
{
    private readonly IReportQueries _reports;
    private readonly IDateTime _clock;

    public GetDailySalesReportQueryHandler(IReportQueries reports, IDateTime clock)
    {
        _reports = reports;
        _clock = clock;
    }

    public async Task<Result<DailySalesReportDto>> Handle(
        GetDailySalesReportQuery request, CancellationToken cancellationToken)
    {
        var date = request.Date ?? _clock.UtcDateToday();

        return Result.Success(await _reports.GetDailySalesReportAsync(date, cancellationToken));
    }
}
