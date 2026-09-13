using PMS.Application.Common.DTOs;
using PMS.Application.Common.Settings;
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

namespace PMS.Application.Features.Stock.Commands.UpdateBatch;

public sealed class UpdateBatchCommandHandler
    : IRequestHandler<UpdateBatchCommand, Result<BatchDto>>
{
    private readonly IRepository<Batch, IApplicationDbContext> _batches;
    private readonly IRepository<Product, IApplicationDbContext> _products;
    private readonly IStockQueries _stock;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly IDateTime _clock;
    private readonly ISettingsService _settings;
    private readonly ILogger<UpdateBatchCommandHandler> _logger;

    public UpdateBatchCommandHandler(
        IRepository<Batch, IApplicationDbContext> batches,
        IRepository<Product, IApplicationDbContext> products,
        IStockQueries stock,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        IDateTime clock,
        ISettingsService settings,
        ILogger<UpdateBatchCommandHandler> logger)
    {
        _batches = batches;
        _products = products;
        _stock = stock;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _settings = settings;
        _logger = logger;
    }

    public async Task<Result<BatchDto>> Handle(
        UpdateBatchCommand request, CancellationToken cancellationToken)
    {
        var batch = await _batches.GetByIdAsync(request.Id, cancellationToken);

        if (batch is null)
        {
            _logger.LogWarning(
                "Batch {BatchId} not updated: not found in this pharmacy", request.Id);

            return Result.Failure<BatchDto>(Error.NotFound(nameof(Batch), request.Id));
        }

        var product = await _products.GetByIdAsync(batch.ProductId, cancellationToken);

        if (product is null)
        {
            // A batch whose product has vanished should be impossible: the foreign key is
            // Restrict and the product's delete is a soft one. If it happens, something has
            // gone wrong at a level this handler cannot fix, and saying so beats a null
            // reference three lines later.
            _logger.LogError(
                "Batch {BatchId} references product {ProductId}, which does not exist in "
                + "this pharmacy. The foreign key should have made this impossible",
                batch.Id, batch.ProductId);

            return Result.Failure<BatchDto>(Error.NotFound(nameof(Product), batch.ProductId));
        }

        // The refusal that makes the read-only quantity field mean something. A client that
        // posts a different quantity is told where quantities are changed, rather than
        // receiving a cheerful 200 for a change that did not happen.
        if (request.QuantityInBaseUnits is { } quantity
            && quantity != batch.QuantityInBaseUnits)
        {
            _logger.LogWarning(
                "Batch {BatchId} not updated: an attempt to change quantity from {Current} "
                + "to {Attempted} through the edit endpoint",
                batch.Id, batch.QuantityInBaseUnits, quantity);

            return Result.Failure<BatchDto>(Error.Validation(
                nameof(UpdateBatchCommand.QuantityInBaseUnits),
                "A batch quantity cannot be changed here. Record a stock adjustment instead, "
                + "so the change is kept with the reason for it."));
        }

        if (product.ProductType == ProductType.Medicine && request.ExpiryDate is null)
        {
            return Result.Failure<BatchDto>(Error.Validation(
                nameof(UpdateBatchCommand.ExpiryDate),
                $"'{product.BrandName}' is a medicine, so its expiry date is required."));
        }

        decimal purchasePricePerBaseUnit;

        try
        {
            purchasePricePerBaseUnit = UnitConversion.PricePerBaseUnit(
                request.PurchasePrice, request.PurchasePriceUnit, product);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<BatchDto>(
                Error.Validation(nameof(UpdateBatchCommand.PurchasePrice), ex.Message));
        }

        // Excluding this batch, so re-saving the form without touching the number is not a
        // collision with itself.
        var clash = await _stock.FindBatchNumberClashAsync(
            batch.ProductId, request.BatchNumber, batch.Id, cancellationToken);

        if (clash is not null)
        {
            _logger.LogWarning(
                "Batch {BatchId} not updated: '{BatchNumber}' is already used by batch "
                + "{OtherBatchId} of '{BrandName}'",
                batch.Id, request.BatchNumber.Trim(), clash.BatchId, product.BrandName);

            return Result.Failure<BatchDto>(Error.Conflict(
                $"Batch '{request.BatchNumber.Trim()}' already exists for "
                + $"'{product.BrandName}'. Batch numbers have to be unique per product."));
        }

        var previousNumber = batch.BatchNumber;
        var previousExpiry = batch.ExpiryDate;
        var previousCost = batch.PurchasePricePerBaseUnit;

        batch.UpdateDetails(
            request.BatchNumber,
            request.ExpiryDate,
            request.ManufactureDate,
            purchasePricePerBaseUnit,
            request.SupplierId,
            request.SupplierNameText,
            request.Notes);

        try
        {
            // Tracked from GetByIdAsync, so saving is enough. Not UpdateAsync: it marks the
            // whole graph Modified, which is harmless here only because no children are
            // loaded — see AdjustBatchCommandHandler for what it costs when they are.
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException ex)
        {
            _logger.LogWarning(
                ex,
                "Batch {BatchId} not updated: '{BatchNumber}' was taken between the check "
                + "and the write",
                batch.Id, request.BatchNumber.Trim());

            return Result.Failure<BatchDto>(Error.Conflict(
                $"Batch '{request.BatchNumber.Trim()}' was just taken by another entry for "
                + $"'{product.BrandName}'. Use a different batch number."));
        }

        // Before-and-after on the three fields worth having a record of. An expiry date or a
        // cost quietly edited is exactly the kind of change somebody will later want to
        // account for, and this endpoint writes no audit row of its own.
        _logger.LogInformation(
            "Batch updated {BatchId} of '{BrandName}': number '{PreviousNumber}' -> "
            + "'{BatchNumber}', expiry {PreviousExpiry} -> {Expiry}, cost per {BaseUnit} "
            + "{PreviousCost} -> {Cost}",
            batch.Id, product.BrandName, previousNumber, batch.BatchNumber,
            previousExpiry?.ToString("yyyy-MM-dd") ?? "none",
            batch.ExpiryDate?.ToString("yyyy-MM-dd") ?? "none",
            product.BaseUnitName, previousCost, batch.PurchasePricePerBaseUnit);

        var window = await _settings.GetIntAsync(
            SettingKeys.ExpiryAlertWindowDays, cancellationToken);

        return StockMapping.ToDto(
            batch, product, includePurchasePrice: true,
            today: _clock.UtcDateToday(), expiringSoonWindowDays: window);
    }
}
