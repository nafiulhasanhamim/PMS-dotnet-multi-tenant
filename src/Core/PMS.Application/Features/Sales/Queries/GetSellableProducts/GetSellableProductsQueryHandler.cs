using MediatR;
using PMS.Application.Common.Billing;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Security;
using PMS.Application.Common.Settings;
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
    private readonly ISettingsService _settings;

    public GetSellableProductsQueryHandler(
        ISaleQueries sales, ICurrentUserService currentUser, ISettingsService settings)
    {
        _sales = sales;
        _currentUser = currentUser;
        _settings = settings;
    }

    public async Task<Result<IReadOnlyList<SellableProductDto>>> Handle(
        GetSellableProductsQuery request, CancellationToken cancellationToken)
    {
        // An Employee who cannot sell antibiotics still sees them in the list, flagged with the
        // reason. Hiding them would have a cashier telling a customer the pharmacy does not
        // stock something that is on the shelf behind them; showing the reason has them fetch
        // the pharmacist, which is the outcome the rule is for.
        //
        // Whether they can sell them at all is now the pharmacy's decision rather than a fixed
        // rule — Module 7. Under Off and Optional this is true for every role, so nothing is
        // greyed and nobody is fetched.
        var role = _currentUser.TenantRole();
        var mode = await _settings.GetEnumAsync<AntibioticPrescriptionMode>(
            SettingKeys.AntibioticPrescriptionMode, cancellationToken);

        var mayDispenseAntibiotics =
            role is not null && BillingPolicy.MaySellAntibiotics(role.Value, mode);

        var limit = request.Limit > 0
            ? Math.Min(request.Limit, BillingPolicy.SellableSearchLimit)
            : BillingPolicy.SellableSearchLimit;

        var results = await _sales.SearchSellableAsync(
            request.Search, mayDispenseAntibiotics, limit, cancellationToken);

        return Result.Success(results);
    }
}
