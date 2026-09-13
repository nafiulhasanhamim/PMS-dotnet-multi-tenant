using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Alerts.Queries.GetExpiredBatches;

/// <summary>Batches already past their expiry date and still holding stock.</summary>
public sealed record GetExpiredBatchesQuery(int Page, int PageSize)
    : IRequest<Result<GridResult<ExpiringBatchDto>>>, ITenantScopedRequest;
