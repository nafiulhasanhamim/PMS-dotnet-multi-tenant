using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.Billing;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Security;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Grid;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Sales.Queries.GetSales;

public sealed class GetSalesQueryHandler
    : IRequestHandler<GetSalesQuery, Result<GridResult<SaleListItemDto>>>
{
    private readonly ISaleQueries _sales;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<GetSalesQueryHandler> _logger;

    public GetSalesQueryHandler(
        ISaleQueries sales,
        ICurrentUserService currentUser,
        ILogger<GetSalesQueryHandler> logger)
    {
        _sales = sales;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<GridResult<SaleListItemDto>>> Handle(
        GetSalesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserGuid;
        var role = _currentUser.TenantRole();

        if (userId is null || role is null)
        {
            return Result.Failure<GridResult<SaleListItemDto>>(
                Error.Unauthorized("Not signed in."));
        }

        // The restriction, not a filter. Applied inside the query so the row count is right
        // too: filtering afterwards would page over somebody else's sales and show an Employee
        // how many there were.
        var restrictTo = role == UserRole.Employee ? userId : null;

        if (restrictTo is not null && request.CashierUserId is { } asked && asked != userId)
        {
            // An Employee sending a cashier filter for somebody else. Not an error worth
            // refusing — the restriction already makes it return nothing of theirs — but it is
            // worth a line, because it is what probing for other people's takings looks like.
            _logger.LogWarning(
                "Employee {UserId} requested the sales list filtered to cashier {AskedFor}; "
                + "restricted to their own regardless",
                userId, asked);
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200
            ? BillingPolicy.SalesPageSize
            : request.PageSize;

        var result = await _sales.ListAsync(
            request.From,
            request.To,
            restrictTo is null ? request.CashierUserId : null,
            request.Status,
            restrictTo,
            page,
            pageSize,
            cancellationToken);

        return Result.Success(result);
    }
}
