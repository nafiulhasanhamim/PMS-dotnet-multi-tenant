using FluentValidation;

namespace PMS.Application.Features.Suppliers.Commands.UpdateSupplier;

public sealed class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator()
    {
        RuleFor(c => c.SupplierId).NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("A supplier needs a name.")
            .MaximumLength(200);

        RuleFor(c => c.Phone)
            .NotEmpty().WithMessage("A phone number is required.")
            .MaximumLength(40);

        RuleFor(c => c.ContactPerson).MaximumLength(200);
        RuleFor(c => c.Email).MaximumLength(256);
        RuleFor(c => c.Address).MaximumLength(500);
        RuleFor(c => c.Company).MaximumLength(200);

        RuleFor(c => c.Email)
            .EmailAddress().WithMessage("That does not look like an email address.")
            .When(c => !string.IsNullOrWhiteSpace(c.Email));
    }
}
