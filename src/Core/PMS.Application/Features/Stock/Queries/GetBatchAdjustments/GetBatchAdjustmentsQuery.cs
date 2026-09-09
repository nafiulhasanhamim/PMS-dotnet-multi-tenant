using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using MediatR;

namespace PMS.Application.Features.Stock.Queries.GetBatchAdjustments;

/// <summary>
/// A page of one batch's adjustment history, newest first.
///
/// <para>Newest first because the question is almost always what just happened to this batch.
/// The audit value of the older rows does not depend on their being at the top.</para>
/// </summary>
public sealed record GetBatchAdjustmentsQuery(Guid BatchId, int Page, int PageSize)
    : IRequest<GridResult<StockAdjustmentDto>>, ITenantScopedRequest;
