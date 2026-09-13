using PMS.Application.Common.DTOs;
using PMS.Application.Common.Settings;
using PMS.Application.Common.Stock;
using PMS.Application.Common.Units;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PMS.Application.Features.Stock.Commands.AdjustBatch;

public sealed class AdjustBatchCommandHandler
    : IRequestHandler<AdjustBatchCommand, Result<BatchAdjustedDto>>
{
    private readonly IRepository<Batch, IApplicationDbContext> _batches;
    private readonly IRepository<Product, IApplicationDbContext> _products;
    private readonly IRepository<StockAdjustment, IApplicationDbContext> _adjustments;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly ISettingsService _settings;
    private readonly ILogger<AdjustBatchCommandHandler> _logger;

    public AdjustBatchCommandHandler(
        IRepository<Batch, IApplicationDbContext> batches,
        IRepository<Product, IApplicationDbContext> products,
        IRepository<StockAdjustment, IApplicationDbContext> adjustments,
        ICurrentUserService currentUser,
        IDateTime clock,
        ISettingsService settings,
        ILogger<AdjustBatchCommandHandler> logger)
    {
        _batches = batches;
        _products = products;
        _adjustments = adjustments;
        _currentUser = currentUser;
        _clock = clock;
        _settings = settings;
        _logger = logger;
    }

    public async Task<Result<BatchAdjustedDto>> Handle(
        AdjustBatchCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserGuid;

        if (userId is null)
        {
            // An adjustment with no author would be an audit row that answers "what" and
            // "why" but not "who", which is the question an owner asks first when a pattern
            // of write-offs appears. Refuse rather than write an anonymous one.
            _logger.LogError(
                "Adjustment refused for batch {BatchId}: the request authenticated but "
                + "carries no user id claim",
                request.BatchId);

            return Result.Failure<BatchAdjustedDto>(
                Error.Unauthorized("Not signed in."));
        }

        var batch = await _batches.GetByIdAsync(request.BatchId, cancellationToken);

        if (batch is null)
        {
            _logger.LogWarning(
                "Batch {BatchId} not adjusted: not found in this pharmacy", request.BatchId);

            return Result.Failure<BatchAdjustedDto>(Error.NotFound(nameof(Batch), request.BatchId));
        }

        var product = await _products.GetByIdAsync(batch.ProductId, cancellationToken);

        if (product is null)
        {
            _logger.LogError(
                "Batch {BatchId} references product {ProductId}, which does not exist in "
                + "this pharmacy",
                batch.Id, batch.ProductId);

            return Result.Failure<BatchAdjustedDto>(
                Error.NotFound(nameof(Product), batch.ProductId));
        }

        // The entered figure in base units. For a correction this is the true total, not a
        // change — see the command.
        int quantityInBaseUnits;

        try
        {
            quantityInBaseUnits = UnitConversion.ToBaseUnits(
                request.Quantity, request.QuantityUnit, product);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            return Result.Failure<BatchAdjustedDto>(
                Error.Validation(nameof(AdjustBatchCommand.Quantity), ex.Message));
        }

        var delta = request.AdjustmentType switch
        {
            AdjustmentType.Add => quantityInBaseUnits,
            AdjustmentType.Remove => -quantityInBaseUnits,
            AdjustmentType.Correction => quantityInBaseUnits - batch.QuantityInBaseUnits,
            _ => 0,
        };

        if (delta == 0)
        {
            // Only reachable for a correction, and only when the figure counted matches what
            // is already recorded. Worth saying plainly: the person has just verified the
            // stock and should be told it agreed, not handed an audit row recording nothing.
            return Result.Failure<BatchAdjustedDto>(Error.Validation(
                nameof(AdjustBatchCommand.Quantity),
                $"Batch '{batch.BatchNumber}' already holds "
                + $"{UnitConversion.FromBaseUnits(batch.QuantityInBaseUnits, product)}, so "
                + "there is nothing to correct."));
        }

        if (batch.QuantityInBaseUnits + delta < 0)
        {
            // Checked here so it becomes a field error naming the actual figures. The entity
            // throws on the same condition and the database has a CHECK constraint behind
            // that; this is the layer that can say something useful about it.
            _logger.LogWarning(
                "Batch {BatchId} not adjusted: a change of {Delta} would take "
                + "{Current} base units below zero",
                batch.Id, delta, batch.QuantityInBaseUnits);

            return Result.Failure<BatchAdjustedDto>(Error.Validation(
                nameof(AdjustBatchCommand.Quantity),
                $"Batch '{batch.BatchNumber}' only holds "
                + $"{UnitConversion.FromBaseUnits(batch.QuantityInBaseUnits, product)}, so "
                + $"{UnitConversion.FromBaseUnits(quantityInBaseUnits, product)} cannot be "
                + "removed from it."));
        }

        if (RequiresExpiringBatchAcknowledgement(request, batch, delta))
        {
            var days = batch.DaysUntilExpiry(_clock.UtcDateToday());

            _logger.LogWarning(
                "Batch {BatchId} not adjusted: adding stock to a batch that expires "
                + "{Expiry} ({Days} days) was not acknowledged",
                batch.Id, batch.ExpiryDate?.ToString("yyyy-MM-dd"), days);

            return Result.Failure<BatchAdjustedDto>(Error.Validation(
                nameof(AdjustBatchCommand.AcknowledgeExpiringBatch),
                days < 0
                    ? $"Batch '{batch.BatchNumber}' expired on "
                      + $"{batch.ExpiryDate:d MMM yyyy}. If new stock has arrived, record it "
                      + "as a new batch so it keeps its own expiry date and cost."
                    : $"Batch '{batch.BatchNumber}' expires on "
                      + $"{batch.ExpiryDate:d MMM yyyy}, in {days} days. If new stock has "
                      + "arrived, record it as a new batch so it keeps its own expiry date "
                      + "and cost."));
        }

        // ── Both rows, or neither ───────────────────────────────────────────────────────
        //
        // Batch.Adjust mutates the quantity and returns the adjustment that explains it, so
        // there is no way to produce one without the other. What follows is how they reach
        // the database together, and it took three attempts to get right — each failure worth
        // recording, because the next module has to write a near-identical path for sales.
        //
        //   1. An explicit BeginTransaction throws. The context enables retry-on-failure, and
        //      SqlServerRetryingExecutionStrategy refuses a user-initiated transaction: it
        //      cannot retry a block whose boundaries it does not control.
        //
        //   2. Saving and letting EF discover the adjustment through the batch's navigation
        //      emits an UPDATE, not an INSERT. When change tracking finds an untracked entity
        //      hanging off a tracked one, it decides the state from whether the key is set —
        //      and this one carries a Guid from its constructor, so EF concludes the row
        //      already exists. The result was an UPDATE against nothing, nought rows
        //      affected, and a TenantId of all zeroes, because the tenant interceptor only
        //      stamps inserts.
        //
        //   3. What works: add the adjustment explicitly, so its state is unambiguously
        //      Added. The repository's AddAsync saves, and that one save carries the pending
        //      quantity change on the tracked batch along with it — one SaveChanges, one
        //      transaction of EF's own making, both rows or neither. Atomic and retriable,
        //      which is the combination wanted.
        var adjustment = batch.Adjust(
            request.AdjustmentType, delta, request.Reason, userId.Value);

        await _adjustments.AddAsync(adjustment, cancellationToken);

        _logger.LogInformation(
            "Batch adjusted {BatchId} '{BatchNumber}' of '{BrandName}': {Type} "
            + "{Before} -> {After} {BaseUnit} (change {Delta}) by {UserId}. Reason: {Reason}",
            batch.Id, batch.BatchNumber, product.BrandName, request.AdjustmentType,
            adjustment.QuantityBeforeInBaseUnits, adjustment.QuantityAfterInBaseUnits,
            product.BaseUnitName, delta, userId.Value, request.Reason.Trim());

        var window = await _settings.GetIntAsync(
            SettingKeys.ExpiryAlertWindowDays, cancellationToken);

        return new BatchAdjustedDto(
            StockMapping.ToDto(
                batch, product, includePurchasePrice: true,
                today: _clock.UtcDateToday(),
                expiringSoonWindowDays: window),
            new StockAdjustmentDto(
                adjustment.Id,
                batch.Id,
                batch.BatchNumber,
                adjustment.AdjustmentType,
                adjustment.QuantityChangeInBaseUnits,
                StockMapping.FormatChange(adjustment.QuantityChangeInBaseUnits, product),
                adjustment.QuantityBeforeInBaseUnits,
                adjustment.QuantityAfterInBaseUnits,
                adjustment.Reason,
                adjustment.AdjustedByUserId,

                // Not resolved on the write path. The caller is the person who just did it,
                // and a join to the global Users table to tell them their own name would be
                // a query for nothing. The history query resolves it.
                AdjustedByName: null,
                adjustment.CreatedOnUtc));
    }

    /// <summary>
    /// Whether this is the wrong-screen case: stock being <em>added</em> to a batch that has
    /// expired or is about to.
    ///
    /// <para>Only additions. Removing stock from an expired batch is the correct and expected
    /// thing to do with it — that is disposal — and a warning there would train people to
    /// click through warnings.</para>
    /// </summary>
    private bool RequiresExpiringBatchAcknowledgement(
        AdjustBatchCommand request, Batch batch, int delta)
    {
        if (request.AcknowledgeExpiringBatch)
        {
            return false;
        }

        // Judged on the sign of the delta, not on the adjustment type. A correction upwards is
        // an addition in everything but name and carries exactly the same risk — it is the
        // other way somebody records fresh stock against an old batch — so keying off the type
        // alone would leave the guard with an obvious hole in it.
        if (delta <= 0)
        {
            return false;
        }

        return batch.DaysUntilExpiry(_clock.UtcDateToday())
            is { } days && days <= StockPolicy.AddToExpiringBatchWarningDays;
    }
}
