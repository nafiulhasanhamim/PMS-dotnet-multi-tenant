using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Suppliers.Commands.CreateSupplier;

public sealed class CreateSupplierCommandHandler
    : IRequestHandler<CreateSupplierCommand, Result<SupplierDetailDto>>
{
    private readonly IRepository<Supplier, IApplicationDbContext> _suppliers;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ILogger<CreateSupplierCommandHandler> _logger;

    public CreateSupplierCommandHandler(
        IRepository<Supplier, IApplicationDbContext> suppliers,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ILogger<CreateSupplierCommandHandler> logger)
    {
        _suppliers = suppliers;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<SupplierDetailDto>> Handle(
        CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        // No duplicate-name check, deliberately. Two distributors can genuinely share a name,
        // and a pharmacy that has recorded the same one twice by accident has a tidying problem
        // rather than a constraint violation - refusing the second would leave them unable to
        // record a real delivery arriving now.
        var supplier = new Supplier(
            request.Name, request.Phone, request.ContactPerson,
            request.Email, request.Address, request.Company);

        await _suppliers.AddAsync(supplier, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Supplier created {SupplierId} '{Name}' ({Phone})",
            supplier.Id, supplier.Name, supplier.Phone);

        // Balance is zero by construction - nothing has been bought from them yet - so this
        // does not go back to the database for it.
        return new SupplierDetailDto(
            supplier.Id, supplier.Name, supplier.Phone, supplier.ContactPerson, supplier.Email,
            supplier.Address, supplier.Company, supplier.IsActive,
            SupplierBalance.Zero, 0, 0);
    }
}
