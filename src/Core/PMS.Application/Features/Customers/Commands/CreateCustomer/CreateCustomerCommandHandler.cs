using Ardalis.GuardClauses;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Mappings;
using PMS.Application.Features.Customers.Specifications;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.ValueObjects;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Customers.Commands.CreateCustomer;

/// <summary>
/// Handler for CreateCustomerCommand.
/// </summary>
public sealed class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, Result<CustomerDto>>
{
    private readonly IRepository<Customer, IApplicationDbContext> _customerRepository;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;

    public CreateCustomerCommandHandler(
        IRepository<Customer, IApplicationDbContext> customerRepository,
        IUnitOfWork<IApplicationDbContext> unitOfWork)
    {
        _customerRepository = Guard.Against.Null(customerRepository);
        _unitOfWork = Guard.Against.Null(unitOfWork);
    }

    public async Task<Result<CustomerDto>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        // Validate email
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<CustomerDto>(emailResult.Error);
        }

        // Check for duplicate email
        var existingCustomer = await _customerRepository.FirstOrDefaultAsync(
            new CustomerByEmailSpec(request.Email), cancellationToken);

        if (existingCustomer is not null)
        {
            return Result.Failure<CustomerDto>(
                Error.Conflict($"Customer with email '{request.Email}' already exists."));
        }

        // Create customer
        var customer = new Customer(request.FirstName, request.LastName, emailResult.Value);

        // Set phone number if provided
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var phoneResult = PhoneNumber.Create(request.PhoneNumber);
            if (phoneResult.IsSuccess)
            {
                customer.UpdatePhoneNumber(phoneResult.Value);
            }
        }

        await _customerRepository.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return customer.ToDto();
    }
}
