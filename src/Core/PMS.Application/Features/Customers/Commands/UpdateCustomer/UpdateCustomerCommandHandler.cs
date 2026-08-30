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

namespace PMS.Application.Features.Customers.Commands.UpdateCustomer;

/// <summary>
/// Handler for UpdateCustomerCommand.
/// </summary>
public sealed class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand, Result<CustomerDto>>
{
    private readonly IRepository<Customer, IApplicationDbContext> _customerRepository;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;

    public UpdateCustomerCommandHandler(
        IRepository<Customer, IApplicationDbContext> customerRepository,
        IUnitOfWork<IApplicationDbContext> unitOfWork)
    {
        _customerRepository = Guard.Against.Null(customerRepository);
        _unitOfWork = Guard.Against.Null(unitOfWork);
    }

    public async Task<Result<CustomerDto>> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        // Get customer
        var customer = await _customerRepository.FirstOrDefaultAsync(
            new CustomerByIdSpec(request.Id), cancellationToken);

        if (customer is null)
        {
            return Result.Failure<CustomerDto>(Error.NotFound("Customer", request.Id));
        }

        // Validate new email
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<CustomerDto>(emailResult.Error);
        }

        // Check for duplicate email (if email changed)
        if (customer.Email.Value != emailResult.Value.Value)
        {
            var existingCustomer = await _customerRepository.FirstOrDefaultAsync(
                new CustomerByEmailSpec(request.Email), cancellationToken);

            if (existingCustomer is not null)
            {
                return Result.Failure<CustomerDto>(
                    Error.Conflict($"Customer with email '{request.Email}' already exists."));
            }
        }

        // Update customer
        customer.UpdateName(request.FirstName, request.LastName);
        customer.UpdateEmail(emailResult.Value);

        // Update phone number
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var phoneResult = PhoneNumber.Create(request.PhoneNumber);
            if (phoneResult.IsSuccess)
            {
                customer.UpdatePhoneNumber(phoneResult.Value);
            }
        }
        else
        {
            customer.UpdatePhoneNumber(null);
        }

        await _customerRepository.UpdateAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return customer.ToDto();
    }
}
