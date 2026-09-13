using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Sales.Queries.GetSellableProducts;

/// <summary>
/// The billing screen type-ahead.
///
/// <para>Carries no role and no "include antibiotics" flag: the caller cannot ask to be treated
/// as a pharmacist. The handler reads the role from the token, which is the only source that
/// cannot be forged by the client.</para>
/// </summary>
public sealed record GetSellableProductsQuery(string? Search, int Limit = 0)
    : IRequest<Result<IReadOnlyList<SellableProductDto>>>, ITenantScopedRequest;
