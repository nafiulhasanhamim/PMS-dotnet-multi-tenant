using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Reports.Queries.GetSalesByProductType;

/// <summary>Revenue and margin split by product type.</summary>
public sealed record GetSalesByProductTypeQuery(DateOnly? From, DateOnly? To)
    : IRequest<Result<IReadOnlyList<SalesByProductTypeRowDto>>>, ITenantScopedRequest;
