using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Suppliers.Commands.UpdateSupplier;

public sealed class UpdateSupplierCommandHandler
    : IRequestHandler<UpdateSupplierCommand, Result<SupplierDetailDto>>
{
    private readonly IRepository<Supplier, IApplicationDbContext> _suppliers;
    private readonly ISupplierQueries _queries;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ILogger<UpdateSupplierCommandHandler> _logger;

    public UpdateSupplierCommandHandler(
        IRepository<Supplier, IApplicationDbContext> suppliers,
        ISupplierQueries queries,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ILogger<UpdateSupplierCommandHandler> logger)
    {
        _suppliers = suppliers;
        _queries = queries;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<SupplierDetailDto>> Handle(
        UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        // Through the repository, so the tenant filter applies: another pharmacy's supplier id
        // is simply not found, and the 404 is the truth rather than a disguised 403.
        var supplier = await _suppliers.GetByIdAsync(request.SupplierId, cancellationToken);

        if (supplier is null)
        {
            return Result.Failure<SupplierDetailDto>(
                Error.NotFound(nameof(Supplier), request.SupplierId));
        }

        supplier.Update(
            request.Name, request.Phone, request.ContactPerson,
            request.Email, request.Address, request.Company);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Supplier updated {SupplierId} '{Name}'", supplier.Id, supplier.Name);

        var detail = await _queries.GetSupplierAsync(supplier.Id, cancellationToken);

        return detail is null
            ? Result.Failure<SupplierDetailDto>(Error.NotFound(nameof(Supplier), supplier.Id))
            : Result.Success(detail);
    }
}
