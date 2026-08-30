using Ardalis.GuardClauses;
using PMS.Application.Features.Customers.Specifications;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;

namespace PMS.Application.Features.Customers.Commands.DeleteCustomer;

/// <summary>
/// Handler for DeleteCustomerCommand.
/// </summary>
public sealed class DeleteCustomerCommandHandler : IRequestHandler<DeleteCustomerCommand, Result>
{
    private readonly IRepository<Customer, IApplicationDbContext> _customerRepository;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;

    public DeleteCustomerCommandHandler(
        IRepository<Customer, IApplicationDbContext> customerRepository,
        IUnitOfWork<IApplicationDbContext> unitOfWork)
    {
        _customerRepository = Guard.Against.Null(customerRepository);
        _unitOfWork = Guard.Against.Null(unitOfWork);
    }

    public async Task<Result> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.FirstOrDefaultAsync(
            new CustomerByIdSpec(request.Id), cancellationToken);

        if (customer is null)
        {
            return Result.Failure(Error.NotFound("Customer", request.Id));
        }

        await _customerRepository.DeleteAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
