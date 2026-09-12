using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Reports.Queries.GetTopSellingProducts;

/// <summary>What moved, net of returns, ordered by quantity, revenue or profit.</summary>
public sealed record GetTopSellingProductsQuery(
    DateOnly? From,
    DateOnly? To,
    TopSellingSort SortBy,
    ProductType? ProductType,
    int Page,
    int PageSize)
    : IRequest<Result<GridResult<TopSellingProductDto>>>, ITenantScopedRequest;
