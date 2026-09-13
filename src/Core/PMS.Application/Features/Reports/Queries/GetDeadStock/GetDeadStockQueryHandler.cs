using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Settings;
using PMS.Application.Common.Stock;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Reports.Queries.GetDeadStock;

public sealed class GetDeadStockQueryHandler
    : IRequestHandler<GetDeadStockQuery, Result<DeadStockPageDto>>
{
    private readonly IReportQueries _reports;
    private readonly ISettingsService _settings;

    public GetDeadStockQueryHandler(IReportQueries reports, ISettingsService settings)
    {
        _reports = reports;
        _settings = settings;
    }

    public async Task<Result<DeadStockPageDto>> Handle(
        GetDeadStockQuery request, CancellationToken cancellationToken)
    {
        // The pharmacy's own threshold since Module 10, and the default when the caller names
        // none.
        var configured = await _settings.GetIntAsync(
            SettingKeys.DeadStockThresholdDays, cancellationToken);

        // Coerced once, here, and both calls below get the same number. Coercing separately in
        // each would let a hand-crafted threshold produce a summary over a different window than
        // the rows it heads.
        var threshold = StockPolicy.CoerceDeadStockThreshold(request.ThresholdDays, configured);

        var rows = await _reports.GetDeadStockAsync(
            threshold, request.ProductType,
            AlertPaging.Page(request.Page), AlertPaging.Size(request.PageSize),
            cancellationToken);

        var summary = await _reports.GetDeadStockSummaryAsync(
            threshold, request.ProductType, cancellationToken);

        return Result.Success(new DeadStockPageDto(threshold, rows, summary));
    }
}
