using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Settings;
using PMS.Application.Common.Stock;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Alerts.Queries.GetExpiringBatches;

/// <summary>
/// One page of batches expiring inside a window.
///
/// <para>A caller may ask for one of the standard windows or for the pharmacy's own; anything
/// else falls back to the configured one rather than being refused, because an unbounded
/// <c>days</c> is a request for the whole catalogue dressed up as an alert query. Since Module 10
/// the fallback is per-pharmacy.</para>
/// </summary>
public sealed class GetExpiringBatchesQueryHandler
    : IRequestHandler<GetExpiringBatchesQuery, Result<GridResult<ExpiringBatchDto>>>
{
    private readonly IAlertQueries _alerts;
    private readonly ISettingsService _settings;

    public GetExpiringBatchesQueryHandler(IAlertQueries alerts, ISettingsService settings)
    {
        _alerts = alerts;
        _settings = settings;
    }

    public async Task<Result<GridResult<ExpiringBatchDto>>> Handle(
        GetExpiringBatchesQuery request, CancellationToken cancellationToken)
    {
        var configured = await _settings.GetIntAsync(
            SettingKeys.ExpiryAlertWindowDays, cancellationToken);

        return Result.Success(await _alerts.GetExpiringBatchesAsync(
            StockPolicy.CoerceExpiryWindow(request.Days, configured),
            AlertPaging.Page(request.Page),
            AlertPaging.Size(request.PageSize),
            cancellationToken));
    }
}
