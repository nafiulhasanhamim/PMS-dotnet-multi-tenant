using MediatR;
using PMS.Application.Common.Billing;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Security;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Sales.Queries.GetSellableProducts;

public sealed class GetSellableProductsQueryHandler
    : IRequestHandler<GetSellableProductsQuery, Result<IReadOnlyList<SellableProductDto>>>
{
    private readonly ISaleQueries _sales;
    private readonly ICurrentUserService _currentUser;

    public GetSellableProductsQueryHandler(
        ISaleQueries sales, ICurrentUserService currentUser)
    {
        _sales = sales;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<SellableProductDto>>> Handle(
        GetSellableProductsQuery request, CancellationToken cancellationToken)
    {
        // An Employee still sees antibiotics in the list, flagged with the reason they cannot
        // sell them. Hiding them would have a cashier telling a customer the pharmacy does not
        // stock something that is on the shelf behind them; showing the reason has them fetch
        // the pharmacist, which is the outcome the rule is for.
        var mayDispenseAntibiotics =
            _currentUser.TenantRole() is UserRole.Admin or UserRole.Pharmacist;

        var limit = request.Limit > 0
            ? Math.Min(request.Limit, BillingPolicy.SellableSearchLimit)
            : BillingPolicy.SellableSearchLimit;

        var results = await _sales.SearchSellableAsync(
            request.Search, mayDispenseAntibiotics, limit, cancellationToken);

        return Result.Success(results);
    }
}
