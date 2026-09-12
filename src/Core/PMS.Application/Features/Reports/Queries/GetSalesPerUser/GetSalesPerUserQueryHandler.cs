using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Reports;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Reports.Queries.GetSalesPerUser;

public sealed class GetSalesPerUserQueryHandler
    : IRequestHandler<GetSalesPerUserQuery, Result<IReadOnlyList<SalesPerUserRowDto>>>
{
    private readonly IReportQueries _reports;
    private readonly IDateTime _clock;

    public GetSalesPerUserQueryHandler(IReportQueries reports, IDateTime clock)
    {
        _reports = reports;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<SalesPerUserRowDto>>> Handle(
        GetSalesPerUserQuery request, CancellationToken cancellationToken)
    {
        var (from, to) = ReportRange.Resolve(request.From, request.To, _clock.UtcDateToday());

        return Result.Success(await _reports.GetSalesPerUserAsync(from, to, cancellationToken));
    }
}
