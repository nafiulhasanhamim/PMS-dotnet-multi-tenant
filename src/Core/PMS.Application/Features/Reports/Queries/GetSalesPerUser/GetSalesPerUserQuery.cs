using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Reports.Queries.GetSalesPerUser;

/// <summary>Per cashier: what they rang up and what they discounted.</summary>
public sealed record GetSalesPerUserQuery(DateOnly? From, DateOnly? To)
    : IRequest<Result<IReadOnlyList<SalesPerUserRowDto>>>, ITenantScopedRequest;
