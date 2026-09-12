using MediatR;
using Microsoft.Extensions.Logging;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.SharedKernel.Interfaces;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Suppliers.Commands.RecordPayment;

/// <summary>
/// Records money moving between the pharmacy and a supplier, in either direction.
///
/// <para>A <b>payment</b> goes out and advances the linked bill's <c>AmountPaid</c> when one is
/// named. A <b>refund</b> comes back, settling a credit; a <b>write-off</b> gives that credit up
/// with no cash moving. Neither of the latter two touches any bill — see the validator.</para>
///
/// <para><b>Everything is validated before anything is written</b>, so the single
/// <c>SaveChanges</c> at the end is the whole transaction. That matters:
/// <c>ExecuteInTransactionAsync</c> commits on return rather than inspecting the result, so a
/// handler that wrote first and refused afterwards would commit the write. Here there is nothing
/// to roll back because nothing is written until every check has passed.</para>
/// </summary>
public sealed class RecordSupplierPaymentCommandHandler
    : IRequestHandler<RecordSupplierPaymentCommand, Result<PaymentRecordedDto>>
{
    private readonly IRepository<Supplier, IApplicationDbContext> _suppliers;
    private readonly IRepository<Purchase, IApplicationDbContext> _purchases;
    private readonly IRepository<SupplierPayment, IApplicationDbContext> _payments;
    private readonly ISupplierBalanceQueries _balances;
    private readonly IUnitOfWork<IApplicationDbContext> _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;
    private readonly ILogger<RecordSupplierPaymentCommandHandler> _logger;

    public RecordSupplierPaymentCommandHandler(
        IRepository<Supplier, IApplicationDbContext> suppliers,
        IRepository<Purchase, IApplicationDbContext> purchases,
        IRepository<SupplierPayment, IApplicationDbContext> payments,
        ISupplierBalanceQueries balances,
        IUnitOfWork<IApplicationDbContext> unitOfWork,
        ICurrentUserService currentUser,
        IDateTime clock,
        ILogger<RecordSupplierPaymentCommandHandler> logger)
    {
        _suppliers = suppliers;
        _purchases = purchases;
        _payments = payments;
        _balances = balances;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<PaymentRecordedDto>> Handle(
        RecordSupplierPaymentCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserGuid;

        if (userId is null)
        {
            return Result.Failure<PaymentRecordedDto>(Error.Unauthorized("Not signed in."));
        }

        var supplier = await _suppliers.GetByIdAsync(request.SupplierId, cancellationToken);

        if (supplier is null)
        {
            return Result.Failure<PaymentRecordedDto>(
                Error.NotFound(nameof(Supplier), request.SupplierId));
        }

        // A deactivated supplier can still be PAID. Stopping a pharmacy from settling a debt to
        // a distributor they have stopped buying from would be the wrong way round entirely -
        // the debt is exactly why the record is still there.
        Purchase? purchase = null;

        if (request.PurchaseId is { } purchaseId)
        {
            purchase = await _purchases.GetByIdAsync(purchaseId, cancellationToken);

            if (purchase is null)
            {
                return Result.Failure<PaymentRecordedDto>(
                    Error.NotFound(nameof(Purchase), purchaseId));
            }

            // Unreachable for a refund or a write-off: the validator and a CHECK constraint both
            // refuse a purchase id on those. This guard is about the payment case.
            if (purchase.SupplierId != supplier.Id)
            {
                // Reachable by hand-crafting a request. Silently accepting it would credit one
                // supplier's bill from another supplier's account.
                return Result.Failure<PaymentRecordedDto>(Error.Validation(
                    nameof(RecordSupplierPaymentCommand.PurchaseId),
                    $"{purchase.PurchaseNumber} is not a purchase from '{supplier.Name}'."));
            }
        }

        // Read BEFORE the payment is written, so the comparison is against what was owed at the
        // moment somebody decided to pay it.
        var balanceBefore = await _balances.GetBalanceAsync(supplier.Id, cancellationToken);

        // What "too much" means depends on which way the money is going. A payment above the
        // outstanding balance overpays; a refund above the credit means the supplier handed back
        // more than they were holding. Both are warned about and neither is refused.
        var incoming = request.Direction != SupplierPaymentDirection.Payment;

        var exceeds = incoming
            ? request.Amount > balanceBefore.CreditAvailable
            : request.Amount > balanceBefore.Outstanding;

        var payment = new SupplierPayment(
            supplier.Id,
            purchase?.Id,
            request.Amount,
            request.PaymentDate ?? _clock.UtcDateToday(),
            string.IsNullOrWhiteSpace(request.PaymentMethod) ? "Cash" : request.PaymentMethod,
            request.Notes,
            userId.Value,
            request.Direction);

        await _payments.AddAsync(payment, cancellationToken);

        // Only a purchase-linked payment OUT touches a bill. A general payment reduces the
        // balance and modifies no individual purchase; a refund or write-off cannot name a bill
        // at all. See the entity for why none of that is an oversight.
        if (!incoming)
        {
            purchase?.ApplyPayment(request.Amount);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "{Direction} {PaymentId} of {Amount} recorded for supplier '{Supplier}' "
            + "({SupplierId}) against {Target}; balance moves from {Before} to {After}",
            request.Direction, payment.Id, payment.Amount, supplier.Name, supplier.Id,
            purchase?.PurchaseNumber ?? "the account",
            balanceBefore.Outstanding,
            incoming
                ? balanceBefore.Outstanding + request.Amount
                : balanceBefore.Outstanding - request.Amount);

        // Recomputed rather than arithmetic on the figure above, so the number the screen shows
        // comes from the same service every other screen reads.
        var balanceAfter = await _balances.GetBalanceAsync(supplier.Id, cancellationToken);

        return new PaymentRecordedDto(
            payment.Id,
            supplier.Id,
            payment.Amount,
            balanceAfter,
            exceeds,
            Warning(request.Direction, exceeds, supplier.Name, balanceBefore, balanceAfter));
    }

    /// <summary>
    /// The non-blocking sentence shown after recording, or null when there is nothing to say.
    ///
    /// <para>Both directions can overshoot, and they overshoot into different situations — one
    /// leaves the pharmacy in credit, the other leaves it owing again. Saying which is the whole
    /// value of the warning.</para>
    /// </summary>
    private static string? Warning(
        SupplierPaymentDirection direction,
        bool exceeds,
        string supplierName,
        SupplierBalance before,
        SupplierBalance after)
    {
        if (!exceeds)
        {
            return null;
        }

        return direction switch
        {
            SupplierPaymentDirection.Refund =>
                $"This is more than the {before.CreditAvailable:0.00} '{supplierName}' was "
                + $"holding, so they are now owed {after.Outstanding:0.00} again.",

            SupplierPaymentDirection.WriteOff =>
                $"This writes off more than the {before.CreditAvailable:0.00} credit on this "
                + $"account, so '{supplierName}' is now owed {after.Outstanding:0.00}.",

            _ =>
                $"This payment is more than the {before.Outstanding:0.00} outstanding. "
                + $"'{supplierName}' is now in credit by "
                + $"{Math.Abs(after.Outstanding):0.00}.",
        };
    }
}
