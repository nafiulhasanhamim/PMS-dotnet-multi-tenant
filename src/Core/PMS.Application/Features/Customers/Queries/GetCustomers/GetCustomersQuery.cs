using PMS.Application.Common.DTOs;
using PMS.Domain.Enums;
using MediatR;

namespace PMS.Application.Features.Customers.Queries.GetCustomers;

/// <summary>
/// Query to get a list of customers with optional filtering and pagination.
/// </summary>
public sealed record GetCustomersQuery(
    string? SearchTerm = null,
    CustomerStatus? Status = null,
    int Page = 1,
    int PageSize = 10) : IRequest<GetCustomersResponse>;

/// <summary>
/// Response for GetCustomersQuery.
/// </summary>
public sealed record GetCustomersResponse(
    List<CustomerSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
