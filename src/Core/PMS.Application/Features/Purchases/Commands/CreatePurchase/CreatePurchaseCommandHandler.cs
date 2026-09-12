using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.DTOs;
using PMS.Application.Features.Stock.Commands.CreateBatch;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Purchases.Commands.CreatePurchase;

/// <summary>
/// Records a purchase and the batches it delivered.
///
/// <para><b>Every batch is created by sending Module 3's <c>CreateBatchCommand</c>.</b> Module 3's
/// own command doc anticipated this: <em>"Module 4 (Purchase) will send this same command rather
/// than writing batches itself. A purchase line is a delivery, and duplicating the conversion, the
/// duplicate-number policy and the loss check would guarantee the two paths drifted."</em> So unit
/// conversion, the medicine-needs-an-expiry rule, the duplicate batch number policy and the
/// sells-at-a-loss warning are not implemented here at all — they happen once, in the place Add
/// Stock uses, and a change to any of them changes both screens together.</para>
///
/// <para><b>One transaction around the whole thing.</b> A purchase number is allocated, then a
/// batch per line, then the purchase and its lines. A failure on the third line rolls back the
/// first two batches, the purchase number and the purchase — leaving no orphan stock and no gap
/// in the sequence. Each inner command calls <c>SaveChanges</c> of its own, which is harmless
/// inside the ambient transaction and is what makes the batch ids available to the lines.</para>
///
/// <para><b>A refusal is thrown, not returned, and that is not stylistic.</b>
/// <c>IUnitOfWork.ExecuteInTransactionAsync</c> commits as soon as the delegate returns — it
/// inspects nothing about the value. Returning a failed <c>Result</c> from inside it would commit
/// every batch created before the failing line, which is precisely the outcome the transaction
/// exists to prevent. An exception is the only signal that reaches the rollback, so the refusal
/// travels as one and is converted back into a <c>Result</c> outside the transaction.</para>
///
/// <para><b>Lines are built after the batches exist</b>, not mutated afterwards. A
/// <c>PurchaseLine</c> for a batch that failed to be created should never be constructed at all,
/// and building the aggregate last also keeps every dependent in the <c>Added</c> state — the EF
/// trap Module 5 documented, where a new entity discovered through a navigation on an already
/// tracked principal is marked <c>Modified</c> and updates nothing.</para>
/// </summary>
public sealed class CreatePurchaseCommandHandler
    : IRequestHandler<CreatePurchaseCommand, Result<PurchaseCreatedDto>>
{
    private readonly IMediator _mediator;
    private readonly IRepository<Supplier, IApplicationDbContext> _suppliers;
    private readonly IRepository<Purchase, IApplicationDbContext> _purchases;
    private readonly IPurchaseNumberGenerator _purchaseNumbers;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly ILogger<CreatePurchaseCommandHandler> _logger;

    public CreatePurchaseCommandHandler(
        IMediator mediator,
        IRepository<Supplier, IApplicationDbContext> suppliers,
        IRepository<Purchase, IApplicationDbContext> purchases,
        IPurchaseNumberGenerator purchaseNumbers,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ICurrentUserService currentUser,
        IDateTime clock,
        ILogger<CreatePurchaseCommandHandler> logger)
    {
        _mediator = mediator;
        _suppliers = suppliers;
        _purchases = purchases;
        _purchaseNumbers = purchaseNumbers;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<PurchaseCreatedDto>> Handle(
        CreatePurchaseCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserGuid;

        if (userId is null)
        {
            _logger.LogError(
                "Purchase refused: the request authenticated but carries no user id (user {UserId})",
                _currentUser.UserId);

            return Result.Failure<PurchaseCreatedDto>(Error.Unauthorized("Not signed in."));
        }

        try
        {
            // Everything inside runs in one transaction, and the delegate loads all of its own
            // state — the execution strategy may run it more than once and clears the change
            // tracker before each attempt. See IUnitOfWork.ExecuteInTransactionAsync.
            return await _unitOfWork.ExecuteInTransactionAsync(
                ct => RecordAsync(request, userId.Value, ct), cancellationToken);
        }
        catch (PurchaseRefusedException refused)
        {
            // The transaction has already rolled back by the time this is caught: nothing was
            // persisted — no purchase, no lines, no batches, and the purchase number is free
            // again. See the class remarks for why this travels as an exception.
            return Result.Failure<PurchaseCreatedDto>(refused.Error);
        }
    }

    private async Task<Result<PurchaseCreatedDto>> RecordAsync(
        CreatePurchaseCommand request, Guid userId, CancellationToken cancellationToken)
    {
        // Loaded through the repository, so the tenant filter applies: another pharmacy's
        // supplier id is not found here at all.
        var supplier = await _suppliers.GetByIdAsync(request.SupplierId, cancellationToken);

        if (supplier is null)
        {
            throw new PurchaseRefusedException(
                Error.NotFound(nameof(Supplier), request.SupplierId));
        }

        if (!supplier.IsActive)
        {
            // Their history stays visible everywhere; what is refused is recording a NEW delivery
            // against a supplier somebody has deliberately stopped buying from.
            throw new PurchaseRefusedException(Error.Validation(
                nameof(CreatePurchaseCommand.SupplierId),
                $"'{supplier.Name}' is no longer an active supplier. Reactivate them first if "
                + "this delivery really came from them."));
        }

        var purchaseDate = request.PurchaseDate ?? _clock.UtcDateToday();

        // Allocated inside the transaction, so a purchase that fails takes its number with it and
        // the sequence has no gaps. See IPurchaseNumberGenerator for why this is not count + 1.
        var purchaseNumber = await _purchaseNumbers.NextAsync(cancellationToken);

        var purchase = new Purchase(
            supplier.Id, purchaseNumber, purchaseDate, userId, request.Notes);

        var warnings = new List<string>();

        for (var index = 0; index < request.Lines.Count; index++)
        {
            var line = request.Lines[index];

            // Module 3, unchanged. Everything it refuses, this refuses — including a duplicate
            // batch number, which is exactly the acceptance criterion that proves the two paths
            // share one implementation.
            var created = await _mediator.Send(
                new CreateBatchCommand(
                    line.ProductId,
                    line.BatchNumber,
                    line.ExpiryDate,
                    line.ManufactureDate,
                    line.Quantity,
                    line.QuantityUnit,
                    line.PurchasePrice,
                    line.PurchasePriceUnit,
                    supplier.Id,

                    // The supplier's name at the time, alongside the id. Module 3 keeps both:
                    // the id is the record, the text is what was written on the delivery note.
                    supplier.Name,
                    line.Notes),
                cancellationToken);

            if (created.IsFailure)
            {
                _logger.LogWarning(
                    "Purchase abandoned at line {LineNumber} of {LineCount} for supplier "
                    + "'{Supplier}': {Error}",
                    index + 1, request.Lines.Count, supplier.Name, created.Error.Description);

                // Throwing rolls back the batches earlier lines already created. The error is
                // re-pointed at the row that caused it so the form can mark the right line.
                throw new PurchaseRefusedException(AtLine(created.Error, index));
            }

            var batch = created.Value!;

            purchase.AddLine(
                line.ProductId,
                batch.Batch.Id,
                batch.Batch.QuantityInBaseUnits,

                // Null cannot happen here — the batch was just created with a purchase price, and
                // the DTO only omits it for a role that may not see costs. Defaulting rather than
                // asserting keeps the nullable annotation honest without inventing a throw.
                batch.Batch.PurchasePricePerBaseUnit ?? 0m);

            if (batch.Warning is { } warning)
            {
                warnings.Add(warning);
            }
        }

        purchase.Settle();

        await _purchases.AddAsync(purchase, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Purchase recorded {PurchaseId} {PurchaseNumber} from '{Supplier}' ({SupplierId}): "
            + "{LineCount} lines totalling {Total}, dated {PurchaseDate}",
            purchase.Id, purchase.PurchaseNumber, supplier.Name, supplier.Id,
            purchase.Lines.Count, purchase.TotalAmount, purchaseDate);

        return new PurchaseCreatedDto(
            purchase.Id,
            purchase.PurchaseNumber,
            purchase.TotalAmount,
            purchase.Lines.Count,
            warnings);
    }

    /// <summary>
    /// Re-points a batch command's error at the purchase line that produced it.
    ///
    /// <para>Without this, a duplicate batch number on the third row comes back attached to
    /// <c>BatchNumber</c> — and the form has four of those. Naming the row is the difference
    /// between a message somebody can act on and one they have to guess at.</para>
    /// </summary>
    private static Error AtLine(Error error, int index)
    {
        var prefix = $"Line {index + 1}: ";

        // A conflict stays a conflict — "with" keeps the code, so a duplicate batch number is
        // still a 409 rather than being demoted to a 400 on the way through.
        return error.Code.StartsWith("Validation.", StringComparison.Ordinal)
            ? Error.Validation($"Lines[{index}]", prefix + error.Description)
            : error with { Description = prefix + error.Description };
    }

    /// <summary>
    /// Carries a refusal out through the transaction boundary.
    ///
    /// <para>Private and caught immediately: it never escapes this handler, and it exists only
    /// because <c>ExecuteInTransactionAsync</c> commits on return and rolls back on throw. See the
    /// class remarks.</para>
    /// </summary>
    private sealed class PurchaseRefusedException(Error error) : Exception(error.Description)
    {
        public Error Error { get; } = error;
    }
}
