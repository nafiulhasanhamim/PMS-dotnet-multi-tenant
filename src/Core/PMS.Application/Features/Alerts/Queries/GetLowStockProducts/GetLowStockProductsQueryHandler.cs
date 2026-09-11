using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Stock;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Alerts.Queries.GetLowStockProducts;

public sealed class GetLowStockProductsQueryHandler
    : IRequestHandler<GetLowStockProductsQuery, Result<GridResult<LowStockProductDto>>>
{
    private readonly IAlertQueries _alerts;

    public GetLowStockProductsQueryHandler(IAlertQueries alerts)
    {
        _alerts = alerts;
    }

    public async Task<Result<GridResult<LowStockProductDto>>> Handle(
        GetLowStockProductsQuery request, CancellationToken cancellationToken)
        => Result.Success(await _alerts.GetLowStockProductsAsync(
            request.ProductType,
            request.Status,
            AlertPaging.Page(request.Page),
            AlertPaging.Size(request.PageSize),
            cancellationToken));
}
