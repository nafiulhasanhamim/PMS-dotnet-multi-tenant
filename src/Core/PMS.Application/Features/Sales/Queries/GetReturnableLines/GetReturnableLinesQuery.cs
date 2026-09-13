using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Sales.Queries.GetReturnableLines;

/// <summary>
/// One sale's lines with what is still returnable on each, and what returning it would refund.
/// </summary>
public sealed record GetReturnableLinesQuery(Guid SaleId)
    : IRequest<Result<ReturnableSaleDto>>, ITenantScopedRequest;
