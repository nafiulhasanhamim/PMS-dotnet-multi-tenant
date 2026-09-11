using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Alerts.Queries.GetExpiringBatches;

/// <summary>
/// Batches expiring inside a window.
/// </summary>
/// <param name="Days">
/// Null, or anything not in the offered set, falls back to the configured window rather than
/// being refused — a stale bookmark or a hand-edited query string should show the default page,
/// not an error. See <c>StockPolicy.CoerceExpiryWindow</c>.
/// </param>
public sealed record GetExpiringBatchesQuery(int? Days, int Page, int PageSize)
    : IRequest<Result<GridResult<ExpiringBatchDto>>>, ITenantScopedRequest;
