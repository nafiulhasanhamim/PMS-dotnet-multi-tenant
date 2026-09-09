using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using MediatR;

namespace PMS.Application.Features.Stock.Queries.GetStock;

/// <summary>
/// One page of the stock list: a row per product, aggregated across its batches.
///
/// <para>Grouped by product rather than listing batches because that is the question being
/// asked. Somebody at the shelf wants to know how much Napa there is; which batch it came in
/// is the next screen, and only matters once they are looking at a specific pack.</para>
///
/// <para>No prices of any kind appear in this result — not the cost, not the sale price — so
/// unlike the product list there is nothing here to withhold from an Employee. The purchase
/// price becomes visible, or not, one level down.</para>
/// </summary>
public sealed record GetStockQuery(
    string? Search,
    StockStatusFilter StockStatus,
    ExpiryStatusFilter ExpiryStatus,
    StockProductTypeFilter ProductType,
    int Page,
    int PageSize) : IRequest<GridResult<StockListItemDto>>, ITenantScopedRequest;
