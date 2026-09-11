using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Stock;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Alerts.Queries.GetExpiringBatches;

public sealed class GetExpiringBatchesQueryHandler
    : IRequestHandler<GetExpiringBatchesQuery, Result<GridResult<ExpiringBatchDto>>>
{
    private readonly IAlertQueries _alerts;

    public GetExpiringBatchesQueryHandler(IAlertQueries alerts)
    {
        _alerts = alerts;
    }

    public async Task<Result<GridResult<ExpiringBatchDto>>> Handle(
        GetExpiringBatchesQuery request, CancellationToken cancellationToken)
        => Result.Success(await _alerts.GetExpiringBatchesAsync(
            StockPolicy.CoerceExpiryWindow(request.Days),
            AlertPaging.Page(request.Page),
            AlertPaging.Size(request.PageSize),
            cancellationToken));
}
