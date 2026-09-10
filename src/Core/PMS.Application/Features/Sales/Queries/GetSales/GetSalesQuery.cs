using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Sales.Queries.GetSales;

/// <summary>
/// One page of the sales list.
///
/// <para><b>There is no "whose sales" parameter, deliberately.</b> An Employee sees only their
/// own, and expressing that as a filter the caller supplies would make it a filter the caller
/// can also remove. The handler derives it from the token.</para>
/// </summary>
/// <param name="CashierUserId">
/// The optional filter an Admin or Pharmacist may apply. Distinct from the restriction above:
/// this one narrows a list somebody is allowed to see in full.
/// </param>
public sealed record GetSalesQuery(
    DateOnly? From,
    DateOnly? To,
    Guid? CashierUserId,
    SaleStatusFilter Status,
    int Page,
    int PageSize)
    : IRequest<Result<GridResult<SaleListItemDto>>>, ITenantScopedRequest;
