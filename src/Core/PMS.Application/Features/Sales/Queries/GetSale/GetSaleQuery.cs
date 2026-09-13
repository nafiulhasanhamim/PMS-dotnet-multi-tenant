using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Sales.Queries.GetSale;

/// <summary>One sale, grouped for the invoice and itemised per batch for staff.</summary>
public sealed record GetSaleQuery(Guid SaleId)
    : IRequest<Result<SaleDetailDto>>, ITenantScopedRequest;
