using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Stock;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Alerts.Queries.GetExpiredBatches;

public sealed class GetExpiredBatchesQueryHandler
    : IRequestHandler<GetExpiredBatchesQuery, Result<GridResult<ExpiringBatchDto>>>
{
    private readonly IAlertQueries _alerts;

    public GetExpiredBatchesQueryHandler(IAlertQueries alerts)
    {
        _alerts = alerts;
    }

    public async Task<Result<GridResult<ExpiringBatchDto>>> Handle(
        GetExpiredBatchesQuery request, CancellationToken cancellationToken)
        => Result.Success(await _alerts.GetExpiredBatchesAsync(
            AlertPaging.Page(request.Page),
            AlertPaging.Size(request.PageSize),
            cancellationToken));
}
