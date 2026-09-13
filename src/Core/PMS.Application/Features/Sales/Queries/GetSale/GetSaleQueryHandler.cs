using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Security;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Sales.Queries.GetSale;

public sealed class GetSaleQueryHandler
    : IRequestHandler<GetSaleQuery, Result<SaleDetailDto>>
{
    private readonly ISaleQueries _sales;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<GetSaleQueryHandler> _logger;

    public GetSaleQueryHandler(
        ISaleQueries sales,
        ICurrentUserService currentUser,
        ILogger<GetSaleQueryHandler> logger)
    {
        _sales = sales;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<SaleDetailDto>> Handle(
        GetSaleQuery request, CancellationToken cancellationToken)
    {
        var sale = await _sales.FindAsync(request.SaleId, cancellationToken);

        if (sale is null)
        {
            // Not found and not-this-pharmacy are the same answer on purpose. Telling a caller
            // that an id exists somewhere else is telling them something about another
            // pharmacy.
            return Result.Failure<SaleDetailDto>(Error.NotFound(nameof(Sale), request.SaleId));
        }

        if (_currentUser.TenantRole() == UserRole.Employee
            && sale.CashierUserId != _currentUser.UserGuid)
        {
            // The own-sales-only rule reaches direct ids too. An Employee who can list only
            // their own sales but read anyone's by id has no restriction at all.
            _logger.LogWarning(
                "Employee {UserId} attempted to read invoice {InvoiceNumber}, rung up by "
                + "{CashierUserId}",
                _currentUser.UserGuid, sale.InvoiceNumber, sale.CashierUserId);

            return Result.Failure<SaleDetailDto>(Error.Forbidden(
                "You can only open sales you rang up yourself."));
        }

        return Result.Success(sale);
    }
}
