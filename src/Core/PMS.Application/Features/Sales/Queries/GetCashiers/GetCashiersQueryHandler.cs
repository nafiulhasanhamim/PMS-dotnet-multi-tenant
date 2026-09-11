using MediatR;
using PMS.Application.Common.Security;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Sales.Queries.GetCashiers;

public sealed class GetCashiersQueryHandler
    : IRequestHandler<GetCashiersQuery, Result<IReadOnlyList<CashierOptionDto>>>
{
    private readonly ISaleQueries _sales;
    private readonly ICurrentUserService _currentUser;

    public GetCashiersQueryHandler(ISaleQueries sales, ICurrentUserService currentUser)
    {
        _sales = sales;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<CashierOptionDto>>> Handle(
        GetCashiersQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.TenantRole() == UserRole.Employee)
        {
            return Result.Success<IReadOnlyList<CashierOptionDto>>([]);
        }

        var cashiers = await _sales.ListCashiersAsync(cancellationToken);

        return Result.Success(cashiers);
    }
}
