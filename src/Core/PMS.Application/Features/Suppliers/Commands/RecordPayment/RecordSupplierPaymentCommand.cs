using MediatR;
using PMS.Application.Common.DTOs;
using PMS.Application.Interfaces;
using PMS.Domain.Enums;
using PMS.SharedKernel.Results;

namespace PMS.Application.Features.Suppliers.Commands.RecordPayment;

/// <summary>
/// Records money paid to a supplier. <b>Admin only.</b>
///
/// <para>Financial settlement is the one thing a Pharmacist cannot do. They can record what
/// arrived and what went back — both are stock events they are present for — but deciding that
/// money has left the till is the owner's. The policy is on the endpoint; this is the reason.</para>
/// </summary>
/// <param name="PurchaseId">
/// Null for a general payment against the account, which reduces the supplier's balance and
/// modifies no individual bill. See <c>SupplierPayment</c> for why that distinction is kept in
/// the data rather than guessed at.
///
/// <para><b>Must be null for a refund or a write-off.</b> A credit belongs to the account, not to
/// one bill, and settling it against a single purchase would mean rewriting that purchase's
/// <c>AmountPaid</c> — a record of money handed over.</para>
/// </param>
/// <param name="Direction">
/// Which way the money went.
///
/// <para>Defaults to <c>Payment</c>, so a caller that predates refunds keeps working unchanged.
/// <c>Refund</c> records the supplier handing a credit back in cash; <c>WriteOff</c> gives that
/// credit up with no money moving.</para>
/// </param>
public sealed record RecordSupplierPaymentCommand(
    Guid SupplierId,
    Guid? PurchaseId,
    decimal Amount,
    DateOnly? PaymentDate,
    string? PaymentMethod,
    string? Notes,
    SupplierPaymentDirection Direction = SupplierPaymentDirection.Payment)
    : IRequest<Result<PaymentRecordedDto>>, ITenantScopedRequest;
