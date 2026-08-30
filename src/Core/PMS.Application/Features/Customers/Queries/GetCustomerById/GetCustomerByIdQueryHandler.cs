using Ardalis.GuardClauses;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Mappings;
using PMS.Application.Features.Customers.Specifications;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Customers.Queries.GetCustomerById;

/// <summary>
/// Handler for GetCustomerByIdQuery.
/// </summary>
public sealed class GetCustomerByIdQueryHandler : IRequestHandler<GetCustomerByIdQuery, Result<CustomerDto>>
{
    private readonly IReadRepository<Customer, IApplicationDbContext> _customerRepository;

    public GetCustomerByIdQueryHandler(IReadRepository<Customer, IApplicationDbContext> customerRepository)
    {
        _customerRepository = Guard.Against.Null(customerRepository);
    }

    public async Task<Result<CustomerDto>> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.FirstOrDefaultAsync(
            new CustomerByIdSpec(request.Id), cancellationToken);

        if (customer is null)
        {
            return Result.Failure<CustomerDto>(Error.NotFound("Customer", request.Id));
        }

        return customer.ToDto();
    }
}
