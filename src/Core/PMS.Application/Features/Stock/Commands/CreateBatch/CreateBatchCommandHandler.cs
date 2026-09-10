using PMS.Application.Common.DTOs;
using PMS.Application.Common.Stock;
using PMS.Application.Common.Units;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.SharedKernel.Common;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Stock.Commands.CreateBatch;

public sealed class CreateBatchCommandHandler
    : IRequestHandler<CreateBatchCommand, Result<BatchCreatedDto>>
{
    private readonly IRepository<Product, IApplicationDbContext> _products;
    private readonly IRepository<Batch, IApplicationDbContext> _batches;
    private readonly IStockQueries _stock;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly IDateTime _clock;
    private readonly ILogger<CreateBatchCommandHandler> _logger;

    public CreateBatchCommandHandler(
        IRepository<Product, IApplicationDbContext> products,
        IRepository<Batch, IApplicationDbContext> batches,
        IStockQueries stock,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        IDateTime clock,
        ILogger<CreateBatchCommandHandler> logger)
    {
        _products = products;
        _batches = batches;
        _stock = stock;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<BatchCreatedDto>> Handle(
        CreateBatchCommand request, CancellationToken cancellationToken)
    {
        // Loaded through the repository, so the tenant query filter applies: another
        // pharmacy's product id is simply not found here, and the 404 that follows is the
        // truth rather than a hidden 403.
        var product = await _products.GetByIdAsync(request.ProductId, cancellationToken);

        if (product is null)
        {
            _logger.LogWarning(
                "Batch not created: product {ProductId} not found in this pharmacy",
                request.ProductId);

            return Result.Failure<BatchCreatedDto>(
                Error.NotFound(nameof(Product), request.ProductId));
        }

        // Rule that a validator cannot express: it depends on a column of another table.
        // Medicines must carry an expiry; a diaper genuinely need not.
        if (product.ProductType == ProductType.Medicine && request.ExpiryDate is null)
        {
            _logger.LogWarning(
                "Batch not created for medicine '{BrandName}': no expiry date supplied",
                product.BrandName);

            return Result.Failure<BatchCreatedDto>(Error.Validation(
                nameof(CreateBatchCommand.ExpiryDate),
                $"'{product.BrandName}' is a medicine, so its expiry date is required."));
        }

        // Conversion, and the two ways it can legitimately fail. Both come back as field
        // errors rather than exceptions: a unit the product does not have means a stale form,
        // and a quantity that does not divide into whole base units is a real answer to give
        // somebody who typed half a strip.
        int quantityInBaseUnits;
        decimal purchasePricePerBaseUnit;

        try
        {
            quantityInBaseUnits = UnitConversion.ToBaseUnits(
                request.Quantity, request.QuantityUnit, product);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            return Result.Failure<BatchCreatedDto>(
                Error.Validation(nameof(CreateBatchCommand.Quantity), ex.Message));
        }

        try
        {
            purchasePricePerBaseUnit = UnitConversion.PricePerBaseUnit(
                request.PurchasePrice, request.PurchasePriceUnit, product);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<BatchCreatedDto>(
                Error.Validation(nameof(CreateBatchCommand.PurchasePrice), ex.Message));
        }

        if (quantityInBaseUnits <= 0)
        {
            // Reachable when a fractional quantity of a large unit truncates to nothing, which
            // the validator's "greater than zero" on the entered figure cannot see.
            return Result.Failure<BatchCreatedDto>(Error.Validation(
                nameof(CreateBatchCommand.Quantity),
                $"That works out to no {product.BaseUnitName} at all."));
        }

        var clash = await _stock.FindBatchNumberClashAsync(
            request.ProductId, request.BatchNumber, excludingBatchId: null, cancellationToken);

        if (clash is not null)
        {
            return Refuse(clash, product, request.BatchNumber);
        }

        var batch = new Batch(
            product.Id,
            request.BatchNumber,
            request.ExpiryDate,
            request.ManufactureDate,
            purchasePricePerBaseUnit,
            quantityInBaseUnits,
            request.SupplierId,
            request.SupplierNameText,
            request.Notes);

        try
        {
            await _batches.AddAsync(batch, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException ex)
        {
            // The check above lost a race. Two deliveries of the same batch number being
            // entered simultaneously is unlikely but not impossible on a shared counter
            // machine, and the constraint is the only thing that can actually decide it.
            _logger.LogWarning(
                ex,
                "Batch '{BatchNumber}' for product {ProductId} lost a race against "
                + "{Constraint}; the number was taken between the check and the insert",
                request.BatchNumber, request.ProductId, ex.ConstraintName ?? "a unique index");

            return Result.Failure<BatchCreatedDto>(Error.Conflict(
                $"Batch '{request.BatchNumber.Trim()}' was just added for "
                + $"'{product.BrandName}' by someone else. Use a different batch number."));
        }

        // No sale price means no comparison to make. An unpriced product cannot sell at a
        // loss because it cannot sell at all - that is what IsSetupComplete records - so this
        // stays false rather than treating a missing price as zero and flagging every batch.
        var sellsAtALoss = product.PricePerBase is { } salePrice
            && purchasePricePerBaseUnit > salePrice;

        _logger.LogInformation(
            "Batch created {BatchId} '{BatchNumber}' for '{BrandName}' ({ProductId}): "
            + "{Quantity} {BaseUnit} at {CostPerBase} each, expires {Expiry}, "
            + "entered as {EnteredQuantity} {EnteredUnit}",
            batch.Id, batch.BatchNumber, product.BrandName, product.Id,
            quantityInBaseUnits, product.BaseUnitName, purchasePricePerBaseUnit,
            batch.ExpiryDate?.ToString("yyyy-MM-dd") ?? "never",
            request.Quantity, request.QuantityUnit);

        if (sellsAtALoss)
        {
            // Information, not Warning: it is a legitimate situation — bought above list price,
            // repricing to follow — and a Warning here would put a routine business event in
            // the same bucket as things that are actually wrong. It is worth a line because a
            // batch that sells at a loss is worth being able to find later.
            _logger.LogInformation(
                "Batch {BatchId} of '{BrandName}' costs {CostPerBase} per {BaseUnit} but "
                + "sells at {SalePerBase} — it would sell at a loss",
                batch.Id, product.BrandName, purchasePricePerBaseUnit,
                product.BaseUnitName, product.PricePerBase);
        }

        var dto = StockMapping.ToDto(
            batch, product, includePurchasePrice: true, today: _clock.UtcDateToday());

        return new BatchCreatedDto(
            dto,
            sellsAtALoss,
            product.PricePerBase,
            product.IsSetupComplete,
            sellsAtALoss
                ? $"This batch costs {purchasePricePerBaseUnit:0.####} per {product.BaseUnitName} "
                  + $"but '{product.BrandName}' sells for {product.PricePerBase:0.####}. "
                  + "It would sell at a loss."
                : null);
    }

    /// <summary>
    /// The duplicate batch number policy, in one place.
    ///
    /// <para>Both outcomes are refusals; they differ in what they tell the person to do, and
    /// that difference is the whole point. Live stock means they are looking at the wrong
    /// screen — the batch is already there and wants editing or adjusting. A depleted batch
    /// means the number is genuinely reusable in the real world but not here, so the message
    /// says so and suggests a suffix rather than leaving somebody to guess why a number that
    /// is plainly not in use is refused.</para>
    ///
    /// <para><b>Never merged into the existing row.</b> Two deliveries with one quantity would
    /// have one expiry and one cost, and both would be wrong.</para>
    /// </summary>
    private Result<BatchCreatedDto> Refuse(
        BatchNumberClash clash, Product product, string batchNumber)
    {
        var number = batchNumber.Trim();

        if (clash.HasStock)
        {
            _logger.LogWarning(
                "Batch not created: '{BatchNumber}' already exists for '{BrandName}' with "
                + "{Quantity} {BaseUnit} in stock (batch {ExistingBatchId})",
                number, product.BrandName, clash.QuantityInBaseUnits,
                product.BaseUnitName, clash.BatchId);

            return Result.Failure<BatchCreatedDto>(Error.Conflict(
                $"Batch '{number}' already exists for '{product.BrandName}' and still has "
                + "stock. Edit that batch, or record this delivery under a different batch "
                + "number."));
        }

        _logger.LogWarning(
            "Batch not created: '{BatchNumber}' exists for '{BrandName}' but is depleted "
            + "(batch {ExistingBatchId}); a distinct number is required",
            number, product.BrandName, clash.BatchId);

        return Result.Failure<BatchCreatedDto>(Error.Conflict(
            $"A batch numbered '{number}' already exists for '{product.BrandName}'. It is "
            + "sold out, but its record is kept as the history behind those sales, so the "
            + $"number cannot be reused. Try '{number}-2' for this delivery."));
    }
}
