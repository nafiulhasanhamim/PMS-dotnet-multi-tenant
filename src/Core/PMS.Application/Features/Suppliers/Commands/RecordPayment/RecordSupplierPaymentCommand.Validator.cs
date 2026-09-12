using FluentValidation;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Suppliers.Commands.RecordPayment;

public sealed class RecordSupplierPaymentCommandValidator
    : AbstractValidator<RecordSupplierPaymentCommand>
{
    public RecordSupplierPaymentCommandValidator()
    {
        RuleFor(c => c.SupplierId).NotEmpty();

        // Zero is not a payment, and a negative one is a purchase return. Both refused here so
        // neither can quietly move a balance the wrong way.
        RuleFor(c => c.Amount)
            .GreaterThan(0).WithMessage("Enter an amount greater than zero.");

        RuleFor(c => c.PaymentMethod).MaximumLength(40);
        RuleFor(c => c.Notes).MaximumLength(1000);

        // A credit belongs to the ACCOUNT. Settling it against one bill would mean reducing that
        // bill's AmountPaid, which records money that genuinely changed hands and must not be
        // rewritten. The screens allocate credits across bills for display instead.
        RuleFor(c => c.PurchaseId)
            .Null()
            .When(c => c.Direction != SupplierPaymentDirection.Payment)
            .WithMessage(
                "Money coming back from a supplier settles their account, not one bill. "
                + "Leave the purchase unselected.");

        // A write-off moves no money, so it needs a reason. Without one the dues report loses a
        // credit and the history cannot say why.
        RuleFor(c => c.Notes)
            .NotEmpty()
            .When(c => c.Direction == SupplierPaymentDirection.WriteOff)
            .WithMessage("Say why this credit is being written off.");

        // NOTE: there is deliberately NO rule that the amount must not exceed the balance, in
        // either direction. Overpaying happens - a rounded cash settlement, an advance against the
        // next delivery, or a payment entered against the wrong bill - and refusing it would leave
        // somebody unable to record money that has genuinely gone. A refund larger than the credit
        // is the same situation mirrored: the supplier handed back too much, and that is a fact to
        // record rather than an input to reject. The handler warns on both.
    }
}
