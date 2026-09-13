using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Grid;
using MediatR;

namespace PMS.Application.Features.Products.Queries.GetProducts;

/// <summary>
/// One page of this pharmacy's products.
///
/// Tenant-scoped: the marker makes TenantValidationBehavior refuse to run it without a
/// resolved pharmacy, and the query filter supplies the WHERE. Nothing here takes a tenant.
/// </summary>
/// <param name="IncludePrices">
/// False for an Employee. The projection then never reads a price, so a price they may not
/// see does not leave the server. Decided by the controller from the caller's role, not by
/// the client.
/// </param>
public sealed record GetProductsQuery(
    ProductListType ListType,
    string? Search,
    ProductStatusFilter Status,
    bool AntibioticOnly,
    ProductType? ProductType,
    bool IncludePrices,
    int Page,
    int PageSize) : IRequest<GridResult<ProductListItemDto>>, ITenantScopedRequest;
