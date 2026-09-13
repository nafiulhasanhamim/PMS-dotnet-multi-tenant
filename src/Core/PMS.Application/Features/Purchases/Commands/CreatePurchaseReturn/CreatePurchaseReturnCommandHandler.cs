using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Purchasing;
using PMS.Application.Common.Units;
using PMS.Application.Features.Purchases.Specifications;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Purchases.Commands.CreatePurchaseReturn;

/// <summary>
/// Books goods back to a supplier: a return row, a stock adjustment, and the batch quantity.
///
/// <para><b>Three writes, one transaction, and the adjustment is not optional.</b> The batch's
/// quantity can only be changed by <c>Batch.Adjust</c>, which returns the audit row it just
/// produced — so "stock moved with no explanation" is not a mistake this handler can make. What
/// the transaction adds is that the return and the adjustment land together.</para>
///
/// <para><b>What this does NOT touch is <c>Purchase.AmountPaid</c>.</b> A return does not un-pay
/// money already handed over; it reduces what is still owed. A bill paid in full and then
/// returned against goes to a negative balance — the supplier owes the pharmacy — and that is
/// the truthful answer rather than an error.</para>
///
/// <para><b>Everything is checked before anything is written.</b>
/// <c>ExecuteInTransactionAsync</c> commits on return rather than inspecting the result, so a
/// refusal after a write would commit that write. Ordering the method this way means a refusal
/// has nothing to roll back.</para>
/// </summary>
public sealed class CreatePurchaseReturnCommandHandler
    : IRequestHandler<CreatePurchaseReturnCommand, Result<PurchaseReturnedDto>>
{
    private readonly IRepository<PurchaseLine, IApplicationDbContext> _lines;
    private readonly IRepository<PurchaseReturn, IApplicationDbContext> _returns;
    private readonly IRepository<StockAdjustment, IApplicationDbContext> _adjustments;
    private readonly ISupplierBalanceQueries _balances;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<CreatePurchaseReturnCommandHandler> _logger;

    public CreatePurchaseReturnCommandHandler(
        IRepository<PurchaseLine, IApplicationDbContext> lines,
        IRepository<PurchaseReturn, IApplicationDbContext> returns,
        IRepository<StockAdjustment, IApplicationDbContext> adjustments,
        ISupplierBalanceQueries balances,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ICurrentUserService currentUser,
        ILogger<CreatePurchaseReturnCommandHandler> logger)
    {
        _lines = lines;
        _returns = returns;
        _adjustments = adjustments;
        _balances = balances;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<PurchaseReturnedDto>> Handle(
        CreatePurchaseReturnCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserGuid;

        if (userId is null)
        {
            return Result.Failure<PurchaseReturnedDto>(Error.Unauthorized("Not signed in."));
        }

        return await _unitOfWork.ExecuteInTransactionAsync(
            ct => ReturnAsync(request, userId.Value, ct), cancellationToken);
    }

    private async Task<Result<PurchaseReturnedDto>> ReturnAsync(
        CreatePurchaseReturnCommand request, Guid userId, CancellationToken cancellationToken)
    {
        // Tracked, because the batch is about to be adjusted. Loaded through the repository so
        // the tenant filter applies - another pharmacy's line is not found here at all. The spec
        // carries the returns, without which the cap below would read zero.
        var line = await _lines.FirstOrDefaultAsync(
            new PurchaseLineForReturnSpec(request.PurchaseId, request.PurchaseLineId),
            cancellationToken);

        if (line is null)
        {
            return Result.Failure<PurchaseReturnedDto>(
                Error.NotFound(nameof(PurchaseLine), request.PurchaseLineId));
        }

        var product = line.Batch.Product;

        int quantityInBaseUnits;

        try
        {
            quantityInBaseUnits = UnitConversion.ToBaseUnits(
                request.Quantity, request.QuantityUnit, product);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            return Result.Failure<PurchaseReturnedDto>(
                Error.Validation(nameof(CreatePurchaseReturnCommand.Quantity), ex.Message));
        }

        if (quantityInBaseUnits <= 0)
        {
            return Result.Failure<PurchaseReturnedDto>(Error.Validation(
                nameof(CreatePurchaseReturnCommand.Quantity),
                $"That works out to no {product.BaseUnitName} at all."));
        }

        // Off the loaded aggregate, not a second query - which is what the spec's Include is for.
        var alreadyReturned = line.ReturnedInBaseUnits;

        // Both caps, from the one place that defines them. The batch cap is the half that gets
        // forgotten: stock that has been sold is not on the shelf to hand back, whatever the
        // bill said.
        var returnable = PurchaseMath.Returnable(
            line.QuantityInBaseUnits, alreadyReturned, line.Batch.QuantityInBaseUnits);

        if (quantityInBaseUnits > returnable)
        {
            var cappedByStock = PurchaseMath.CappedByStock(
                line.QuantityInBaseUnits, alreadyReturned, line.Batch.QuantityInBaseUnits);

            // The message says WHICH limit was hit, because the two are different problems:
            // "you already sent most of this back" is a records question and "it has been sold"
            // is a stock one.
            var because = cappedByStock
                ? $"Batch '{line.Batch.BatchNumber}' only holds "
                  + $"{UnitConversion.FromBaseUnits(line.Batch.QuantityInBaseUnits, product)} now"
                : $"{UnitConversion.FromBaseUnits(alreadyReturned, product)} of this line has "
                  + "already gone back";

            return Result.Failure<PurchaseReturnedDto>(Error.Validation(
                nameof(CreatePurchaseReturnCommand.Quantity),
                returnable == 0
                    ? $"Nothing on this line can be returned. {because}."
                    : $"At most {UnitConversion.FromBaseUnits(returnable, product)} can be "
                      + $"returned from this line. {because}."));
        }

        // ── Nothing above this line writes. Everything below does. ────────────────────────

        // Batch.Adjust is the only thing that can change a quantity, and it hands back the audit
        // row that explains the change. The reason is tagged so the batch history reads as one
        // event with the return rather than as an unexplained write-off.
        var adjustment = line.Batch.Adjust(
            AdjustmentType.Remove,
            -quantityInBaseUnits,
            PurchaseMath.AdjustmentReason(request.Reason),
            userId);

        await _adjustments.AddAsync(adjustment, cancellationToken);

        var purchaseReturn = new PurchaseReturn(
            line.Id,
            line.BatchId,
            quantityInBaseUnits,

            // The batch's cost, which is the line's by construction - the batch was created from
            // this line. Taking it from the batch is what the module brief specifies.
            line.Batch.PurchasePricePerBaseUnit,
            request.Reason,
            userId);

        await _returns.AddAsync(purchaseReturn, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var balance = await _balances.GetBalanceAsync(
            line.Purchase.SupplierId, cancellationToken);

        _logger.LogInformation(
            "Purchase return {ReturnId}: {Quantity} base units of '{Product}' from batch "
            + "'{BatchNumber}' on {PurchaseNumber}, crediting {Amount}; batch now holds "
            + "{BatchQuantity}",
            purchaseReturn.Id, quantityInBaseUnits, product.BrandName, line.Batch.BatchNumber,
            line.Purchase.PurchaseNumber, purchaseReturn.ReturnAmount,
            line.Batch.QuantityInBaseUnits);

        return new PurchaseReturnedDto(
            purchaseReturn.Id,
            line.Id,
            line.BatchId,
            quantityInBaseUnits,
            UnitConversion.FromBaseUnits(quantityInBaseUnits, product),
            purchaseReturn.ReturnAmount,
            line.Batch.QuantityInBaseUnits,
            UnitConversion.FromBaseUnits(line.Batch.QuantityInBaseUnits, product),
            balance.Outstanding);
    }
}
