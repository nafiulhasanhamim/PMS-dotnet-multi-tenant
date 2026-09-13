using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Reports.Queries.GetMonthlySalesReport;

public sealed class GetMonthlySalesReportQueryHandler
    : IRequestHandler<GetMonthlySalesReportQuery, Result<MonthlySalesReportDto>>
{
    private readonly IReportQueries _reports;
    private readonly IDateTime _clock;

    public GetMonthlySalesReportQueryHandler(IReportQueries reports, IDateTime clock)
    {
        _reports = reports;
        _clock = clock;
    }

    public async Task<Result<MonthlySalesReportDto>> Handle(
        GetMonthlySalesReportQuery request, CancellationToken cancellationToken)
    {
        var today = _clock.UtcDateToday();

        return Result.Success(await _reports.GetMonthlySalesReportAsync(
            request.Month ?? today.Month, request.Year ?? today.Year, cancellationToken));
    }
}
