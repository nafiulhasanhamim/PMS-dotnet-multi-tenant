using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Stock;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Alerts.Queries.GetAlertSummary;

public sealed class GetAlertSummaryQueryHandler
    : IRequestHandler<GetAlertSummaryQuery, Result<AlertSummaryDto>>
{
    private readonly IAlertQueries _alerts;

    public GetAlertSummaryQueryHandler(IAlertQueries alerts)
    {
        _alerts = alerts;
    }

    public async Task<Result<AlertSummaryDto>> Handle(
        GetAlertSummaryQuery request, CancellationToken cancellationToken)
        => Result.Success(await _alerts.GetAlertSummaryAsync(
            StockPolicy.ExpiringSoonWindowDays, cancellationToken));
}
