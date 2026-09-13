using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Suppliers.Commands.SetSupplierStatus;

public sealed class SetSupplierStatusCommandHandler
    : IRequestHandler<SetSupplierStatusCommand, Result<SupplierDetailDto>>
{
    private readonly IRepository<Supplier, IApplicationDbContext> _suppliers;
    private readonly ISupplierQueries _queries;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ILogger<SetSupplierStatusCommandHandler> _logger;

    public SetSupplierStatusCommandHandler(
        IRepository<Supplier, IApplicationDbContext> suppliers,
        ISupplierQueries queries,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ILogger<SetSupplierStatusCommandHandler> logger)
    {
        _suppliers = suppliers;
        _queries = queries;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<SupplierDetailDto>> Handle(
        SetSupplierStatusCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _suppliers.GetByIdAsync(request.SupplierId, cancellationToken);

        if (supplier is null)
        {
            return Result.Failure<SupplierDetailDto>(
                Error.NotFound(nameof(Supplier), request.SupplierId));
        }

        if (request.IsActive)
        {
            supplier.Reactivate();
        }
        else
        {
            supplier.Deactivate();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Supplier {SupplierId} '{Name}' set {Status}",
            supplier.Id, supplier.Name, request.IsActive ? "active" : "inactive");

        var detail = await _queries.GetSupplierAsync(supplier.Id, cancellationToken);

        return detail is null
            ? Result.Failure<SupplierDetailDto>(Error.NotFound(nameof(Supplier), supplier.Id))
            : Result.Success(detail);
    }
}
