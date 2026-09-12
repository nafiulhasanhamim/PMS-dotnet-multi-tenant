using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Reports.Queries.GetStockValuation;

/// <summary>
/// What the shelves are worth at cost, right now. No date range: there is no history of stock
/// levels to look back through, so "as at last month" is a question this system cannot answer.
/// </summary>
public sealed record GetStockValuationQuery(ProductType? ProductType, int Page, int PageSize)
    : IRequest<Result<StockValuationPageDto>>, ITenantScopedRequest;
