using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Stock.Queries.GetProductStock;

/// <summary>
/// One product's stock: the summary, its live batches in FEFO order, and a page of its
/// depleted ones.
/// </summary>
/// <param name="IncludePurchasePrices">
/// False for an Employee, decided by the controller from the token. The projection then never
/// reads the cost column, so it does not cross the wire.
/// </param>
/// <param name="DepletedPage">
/// Depleted batches are paginated and live batches are not, which looks inconsistent and is
/// not: a product holds a handful of live batches at any moment, and accumulates depleted ones
/// for as long as the pharmacy trades.
/// </param>
public sealed record GetProductStockQuery(
    Guid ProductId,
    bool IncludePurchasePrices,
    int DepletedPage = 1,
    int DepletedPageSize = 10) : IRequest<Result<ProductStockDto>>, ITenantScopedRequest;
