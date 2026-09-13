using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Alerts.Queries.GetLowStockProducts;

/// <summary>Active products at or below their reorder level.</summary>
public sealed record GetLowStockProductsQuery(
    ProductType? ProductType,
    LowStockStatusFilter Status,
    int Page,
    int PageSize)
    : IRequest<Result<GridResult<LowStockProductDto>>>, ITenantScopedRequest;
