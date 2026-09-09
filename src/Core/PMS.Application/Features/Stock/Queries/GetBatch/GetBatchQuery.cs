using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Stock.Queries.GetBatch;

/// <summary>
/// One batch, for the edit and adjust screens.
///
/// <para>404 for another pharmacy's batch id, which is the truth rather than a disguised 403:
/// behind the query filter the row genuinely is not there.</para>
/// </summary>
public sealed record GetBatchQuery(Guid BatchId, bool IncludePurchasePrices)
    : IRequest<Result<BatchDto>>, ITenantScopedRequest;
