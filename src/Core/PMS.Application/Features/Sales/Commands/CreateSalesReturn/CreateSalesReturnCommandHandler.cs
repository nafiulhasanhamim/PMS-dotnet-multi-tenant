using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.DTOs;
using PMS.Application.Common.Units;
using PMS.Application.Features.Sales.Specifications;
using PMS.Application.Interfaces;
using PMS.Domain.Billing;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Sales.Commands.CreateSalesReturn;

/// <summary>
/// Records a return: the row, the refund, the stock adjustment, and the batch it goes back to.
/// All four in one transaction.
///
/// <para><b>The refund comes off <c>NetLineTotal</c>, and that is the single most important
/// line in this file.</b> Refunding from <c>LineTotal</c> would pay back the pre-discount price
/// and hand the customer money the pharmacy never took — on every discounted sale, invisibly,
/// and more generously the bigger the discount was. See <c>SaleMath.RefundFor</c>.</para>
///
/// <para><b>Stock goes back to the batch the line came out of</b>, not to whichever batch FEFO
/// would offer next. Nobody is reading batch stickers on returned goods, so this is a
/// bookkeeping convention rather than a physical claim — but it is the convention that keeps
/// each batch's expiry date and purchase cost attached to the right units, which is what expiry
/// alerts and profit reporting both depend on.</para>
/// </summary>
public sealed class CreateSalesReturnCommandHandler
    : IRequestHandler<CreateSalesReturnCommand, Result<SalesReturnedDto>>
{
    private readonly IRepository<Sale, IApplicationDbContext> _sales;
    private readonly IRepository<Batch, IApplicationDbContext> _batches;
    private readonly IRepository<Product, IApplicationDbContext> _products;
    private readonly IRepository<SalesReturn, IApplicationDbContext> _returns;
    private readonly IRepository<StockAdjustment, IApplicationDbContext> _adjustments;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<CreateSalesReturnCommandHandler> _logger;

    public CreateSalesReturnCommandHandler(
        IRepository<Sale, IApplicationDbContext> sales,
        IRepository<Batch, IApplicationDbContext> batches,
        IRepository<Product, IApplicationDbContext> products,
        IRepository<SalesReturn, IApplicationDbContext> returns,
        IRepository<StockAdjustment, IApplicationDbContext> adjustments,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ICurrentUserService currentUser,
        ILogger<CreateSalesReturnCommandHandler> logger)
    {
        _sales = sales;
        _batches = batches;
        _products = products;
        _returns = returns;
        _adjustments = adjustments;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<SalesReturnedDto>> Handle(
        CreateSalesReturnCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserGuid;

        if (userId is null)
        {
            _logger.LogError(
                "Return refused for line {SaleLineId}: the request authenticated but carries "
                + "no user id claim",
                request.SaleLineId);

            return Result.Failure<SalesReturnedDto>(Error.Unauthorized("Not signed in."));
        }

        return await _unitOfWork.ExecuteInTransactionAsync(
            ct => ReturnAsync(request, userId.Value, ct), cancellationToken);
    }

    private async Task<Result<SalesReturnedDto>> ReturnAsync(
        CreateSalesReturnCommand request, Guid userId, CancellationToken ct)
    {
        var sale = await _sales.FirstOrDefaultAsync(new SaleWithLinesSpec(request.SaleId), ct);

        if (sale is null)
        {
            _logger.LogWarning(
                "Return refused: sale {SaleId} not found in this pharmacy", request.SaleId);

            return Result.Failure<SalesReturnedDto>(Error.NotFound(nameof(Sale), request.SaleId));
        }

        if (sale.Status == SaleStatus.Cancelled)
        {
            // The cancellation already put this stock back and reversed the whole amount.
            // Accepting a return on top would restore the stock twice and refund twice.
            _logger.LogWarning(
                "Return refused: invoice {InvoiceNumber} was cancelled on {CancelledAt}",
                sale.InvoiceNumber, sale.CancelledAt);

            return Result.Failure<SalesReturnedDto>(Error.Conflict(
                $"Invoice {sale.InvoiceNumber} was cancelled, which already restored its stock "
                + "and reversed the payment. There is nothing left to return."));
        }

        var line = sale.Lines.FirstOrDefault(l => l.Id == request.SaleLineId);

        if (line is null)
        {
            _logger.LogWarning(
                "Return refused: line {SaleLineId} does not belong to invoice {InvoiceNumber}",
                request.SaleLineId, sale.InvoiceNumber);

            return Result.Failure<SalesReturnedDto>(
                Error.NotFound(nameof(SaleLine), request.SaleLineId));
        }

        var product = await _products.GetByIdAsync(line.ProductId, ct);

        if (product is null)
        {
            _logger.LogError(
                "Return refused: line {SaleLineId} references product {ProductId}, which is "
                + "not in this pharmacy",
                line.Id, line.ProductId);

            return Result.Failure<SalesReturnedDto>(
                Error.NotFound(nameof(Product), line.ProductId));
        }

        int quantityInBaseUnits;

        try
        {
            quantityInBaseUnits = UnitConversion.ToBaseUnits(
                request.Quantity, request.QuantityUnit, product);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            return Result.Failure<SalesReturnedDto>(
                Error.Validation(nameof(CreateSalesReturnCommand.Quantity), ex.Message));
        }

        if (quantityInBaseUnits > line.ReturnableInBaseUnits)
        {
            // Named in the product's own units, because "12" means nothing to somebody holding
            // a strip. The entity throws on the same condition; this is the layer that can say
            // something a cashier can act on.
            _logger.LogWarning(
                "Return refused: {Requested} base units against line {SaleLineId} of invoice "
                + "{InvoiceNumber}, which sold {Sold} and has already had {Returned} back",
                quantityInBaseUnits, line.Id, sale.InvoiceNumber,
                line.QuantityInBaseUnits, line.ReturnedInBaseUnits);

            return Result.Failure<SalesReturnedDto>(Error.Validation(
                nameof(CreateSalesReturnCommand.Quantity),
                line.ReturnedInBaseUnits > 0
                    ? $"Only {UnitConversion.FromBaseUnits(line.ReturnableInBaseUnits, product)} "
                      + $"of '{product.BrandName}' can still be returned on this line — "
                      + $"{UnitConversion.FromBaseUnits(line.ReturnedInBaseUnits, product)} "
                      + "already came back."
                    : $"Only {UnitConversion.FromBaseUnits(line.QuantityInBaseUnits, product)} "
                      + $"of '{product.BrandName}' were sold on this line."));
        }

        var batch = await _batches.GetByIdAsync(line.BatchId, ct);

        if (batch is null)
        {
            _logger.LogError(
                "Return refused: line {SaleLineId} references batch {BatchId}, which is not in "
                + "this pharmacy",
                line.Id, line.BatchId);

            return Result.Failure<SalesReturnedDto>(Error.NotFound(nameof(Batch), line.BatchId));
        }

        var refund = SaleMath.RefundFor(
            quantityInBaseUnits,
            line.QuantityInBaseUnits,
            line.NetLineTotal,
            line.ReturnedInBaseUnits,
            line.RefundedAmount);

        // ── The order of these five lines matters, and it took a failed run to find out ──
        //
        // The repository AddAsync saves internally, so whichever entity is added first decides
        // what is pending at the first save. Adding the return first put the adjustment - by
        // then already sitting in the Batch private collection, because Batch.Adjust puts it
        // there - into that save as an entity discovered through a navigation on an
        // already-tracked, Unchanged principal. EF decides such an entity state from whether
        // its key is set, and the key comes from the constructor, so it concluded the row
        // existed and emitted an UPDATE against nothing: "expected to affect 1 row(s), but
        // actually affected 0".
        //
        // This is Module 3's trap wearing different clothes, and it is worth knowing why
        // CompleteSale does not have it: there the Sale is Added, and dependents found through
        // the navigations of an Added principal are marked Added too. The trap only bites when
        // the principal is already tracked and unchanged - which the batch always is here.
        //
        // So: the adjustment is added first, unambiguously Added, and the return is constructed
        // afterwards. Both saves are inside the transaction, so they still commit together.
        var adjustment = batch.Adjust(
            AdjustmentType.Add,
            quantityInBaseUnits,
            $"Sales return: {request.Reason.Trim()}",
            userId);

        await _adjustments.AddAsync(adjustment, ct);

        var salesReturn = SalesReturn.Record(
            line, quantityInBaseUnits, request.Reason, refund, userId);

        await _returns.AddAsync(salesReturn, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Sales return {ReturnId} against invoice {InvoiceNumber} line {SaleLineId} by "
            + "{UserId}: {Quantity} base units of '{BrandName}' back into batch "
            + "'{BatchNumber}' (now {BatchQuantity}), refund {Refund} of a net line total of "
            + "{NetLineTotal}{Completing}. Reason: {Reason}",
            salesReturn.Id, sale.InvoiceNumber, line.Id, userId, quantityInBaseUnits,
            product.BrandName, batch.BatchNumber, batch.QuantityInBaseUnits, refund,
            line.NetLineTotal,
            line.ReturnableInBaseUnits == 0 ? " (line now fully returned)" : string.Empty,
            request.Reason.Trim());

        return new SalesReturnedDto(
            salesReturn.Id,
            line.Id,
            sale.InvoiceNumber,
            product.BrandName,
            quantityInBaseUnits,
            UnitConversion.FromBaseUnits(quantityInBaseUnits, product),
            refund,
            batch.BatchNumber,
            batch.QuantityInBaseUnits);
    }
}
