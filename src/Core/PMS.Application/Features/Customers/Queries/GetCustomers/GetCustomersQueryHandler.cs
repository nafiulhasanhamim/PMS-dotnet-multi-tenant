using Ardalis.GuardClauses;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Mappings;
using PMS.Application.Features.Customers.Specifications;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using MediatR;

namespace PMS.Application.Features.Customers.Queries.GetCustomers;

/// <summary>
/// Handler for GetCustomersQuery.
/// </summary>
public sealed class GetCustomersQueryHandler : IRequestHandler<GetCustomersQuery, GetCustomersResponse>
{
    private readonly IReadRepository<Customer, IApplicationDbContext> _customerRepository;

    public GetCustomersQueryHandler(IReadRepository<Customer, IApplicationDbContext> customerRepository)
    {
        _customerRepository = Guard.Against.Null(customerRepository);
    }

    public async Task<GetCustomersResponse> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        // Get total count
        var countSpec = new CustomersListSpec(request.SearchTerm, request.Status);
        var totalCount = await _customerRepository.CountAsync(countSpec, cancellationToken);

        // Get paginated results
        var skip = (request.Page - 1) * request.PageSize;
        var spec = new CustomersListSpec(request.SearchTerm, request.Status, skip, request.PageSize);
        var customers = await _customerRepository.ListAsync(spec, cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new GetCustomersResponse(
            customers.Select(c => c.ToSummaryDto()).ToList(),
            totalCount,
            request.Page,
            request.PageSize,
            totalPages);
    }
}
