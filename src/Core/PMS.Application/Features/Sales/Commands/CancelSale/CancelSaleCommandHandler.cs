using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Features.Sales.Specifications;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Sales.Commands.CancelSale;

/// <summary>
/// Cancels a sale and puts its stock back on the shelf.
///
/// <para><b>Every restored unit gets a <see cref="StockAdjustment"/>, unlike the deduction that
/// sold it.</b> The asymmetry is deliberate. A sale is explained by its own invoice, so a second
/// audit row would be noise. Stock <em>reappearing</em> has no such explanation attached to it —
/// somebody counting the shelf next week needs to know why there are forty more tablets than the
/// sales say there should be. So the reversal is tagged "Sale cancelled: {reason}" and shows up
/// in the batch's history where they will look.</para>
///
/// <para><b>Stock already returned is not restored twice.</b> The specification says a
/// cancellation restores the stock from every line, and it does — but a line that has had ten of
/// its forty units returned already had those ten put back, with their own adjustment row. Adding
/// forty here would invent ten tablets. Only what is still outstanding on each line comes back,
/// and the response says how many lines were affected by that so it is visible rather than
/// surprising.</para>
/// </summary>
public sealed class CancelSaleCommandHandler
    : IRequestHandler<CancelSaleCommand, Result<CancelledSaleDto>>
{
    private readonly IRepository<Sale, IApplicationDbContext> _sales;
    private readonly IRepository<Batch, IApplicationDbContext> _batches;
    private readonly IRepository<StockAdjustment, IApplicationDbContext> _adjustments;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly ILogger<CancelSaleCommandHandler> _logger;

    public CancelSaleCommandHandler(
        IRepository<Sale, IApplicationDbContext> sales,
        IRepository<Batch, IApplicationDbContext> batches,
        IRepository<StockAdjustment, IApplicationDbContext> adjustments,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ICurrentUserService currentUser,
        IDateTime clock,
        ILogger<CancelSaleCommandHandler> logger)
    {
        _sales = sales;
        _batches = batches;
        _adjustments = adjustments;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<CancelledSaleDto>> Handle(
        CancelSaleCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserGuid;

        if (userId is null)
        {
            _logger.LogError(
                "Cancellation refused for sale {SaleId}: the request authenticated but carries "
                + "no user id claim",
                request.SaleId);

            return Result.Failure<CancelledSaleDto>(Error.Unauthorized("Not signed in."));
        }

        return await _unitOfWork.ExecuteInTransactionAsync(
            ct => CancelAsync(request, userId.Value, ct), cancellationToken);
    }

    private async Task<Result<CancelledSaleDto>> CancelAsync(
        CancelSaleCommand request, Guid userId, CancellationToken ct)
    {
        var sale = await _sales.FirstOrDefaultAsync(new SaleWithLinesSpec(request.SaleId), ct);

        if (sale is null)
        {
            // Which is also what another pharmacy's sale id looks like from here.
            _logger.LogWarning(
                "Sale {SaleId} not cancelled: not found in this pharmacy", request.SaleId);

            return Result.Failure<CancelledSaleDto>(Error.NotFound(nameof(Sale), request.SaleId));
        }

        if (sale.Status == SaleStatus.Cancelled)
        {
            _logger.LogWarning(
                "Sale {InvoiceNumber} ({SaleId}) not cancelled: already cancelled on "
                + "{CancelledAt} by {CancelledBy}. Reason given then: {PreviousReason}",
                sale.InvoiceNumber, sale.Id, sale.CancelledAt, sale.CancelledByUserId,
                sale.CancelledReason);

            return Result.Failure<CancelledSaleDto>(Error.Conflict(
                $"Invoice {sale.InvoiceNumber} was already cancelled. Cancelling it twice "
                + "would restore its stock twice."));
        }

        var reason = $"Sale cancelled: {request.Reason.Trim()}";

        var batchIds = sale.Lines.Select(line => line.BatchId).Distinct().ToList();
        var batches = await _batches.ListAsync(new BatchesByIdsSpec(batchIds), ct);
        var batchById = batches.ToDictionary(batch => batch.Id);

        var adjustments = new List<StockAdjustment>();
        var restored = 0;
        var linesRestored = 0;
        var skipped = 0;

        foreach (var line in sale.Lines)
        {
            var outstanding = line.ReturnableInBaseUnits;

            if (outstanding <= 0)
            {
                // Fully returned already. Its stock is back and its adjustment row exists.
                skipped++;
                continue;
            }

            if (outstanding < line.QuantityInBaseUnits)
            {
                skipped++;
            }

            if (!batchById.TryGetValue(line.BatchId, out var batch))
            {
                // A batch a sale line points at cannot go missing: the foreign key forbids
                // deleting it. Refuse rather than silently cancel the sale and lose the stock,
                // and log loudly because it means the database has been edited by hand.
                _logger.LogError(
                    "Sale {InvoiceNumber} not cancelled: line {LineId} references batch "
                    + "{BatchId}, which is not in this pharmacy",
                    sale.InvoiceNumber, line.Id, line.BatchId);

                return Result.Failure<CancelledSaleDto>(
                    Error.NotFound(nameof(Batch), line.BatchId));
            }

            // Collected rather than saved one at a time: the repository AddAsync saves
            // internally, and a save per line inside the transaction would be a round trip per
            // line for no benefit.
            adjustments.Add(batch.Adjust(AdjustmentType.Add, outstanding, reason, userId));

            restored += outstanding;
            linesRestored++;
        }

        sale.Cancel(request.Reason, userId, _clock.UtcNow);

        // Explicitly, so their state is unambiguously Added. Left to navigation discovery, EF
        // decides state from whether the key is set — and it is, from the constructor — so it
        // would emit an UPDATE against rows that do not exist. Module 3 hit this; the note in
        // AdjustBatchCommandHandler has the detail.
        if (adjustments.Count > 0)
        {
            await _adjustments.AddRangeAsync(adjustments, ct);
        }

        // Catches the sale status change and the batch quantities when there were no
        // adjustments to add — a sale whose every line had already been returned.
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Sale cancelled {InvoiceNumber} ({SaleId}) by {UserId}: net {NetTotal} reversed, "
            + "{RestoredUnits} base units restored across {LinesRestored} of {LineCount} lines"
            + "{Skipped}. Reason: {Reason}",
            sale.InvoiceNumber, sale.Id, userId, sale.NetTotal, restored, linesRestored,
            sale.Lines.Count,
            skipped > 0 ? $" ({skipped} line(s) partly or wholly returned already)" : string.Empty,
            request.Reason.Trim());

        return new CancelledSaleDto(
            sale.Id, sale.InvoiceNumber, sale.NetTotal, linesRestored, restored, skipped);
    }
}
