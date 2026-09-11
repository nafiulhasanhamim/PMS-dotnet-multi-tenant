using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Sales.Queries.GetCashiers;

/// <summary>
/// Who appears as a cashier in this pharmacy's sales, for the list filter.
///
/// <para>Empty for an Employee rather than refused: they have nothing to filter by, since they
/// see only their own sales, and an empty list lets the page simply not render the control.</para>
/// </summary>
public sealed record GetCashiersQuery
    : IRequest<Result<IReadOnlyList<CashierOptionDto>>>, ITenantScopedRequest;
