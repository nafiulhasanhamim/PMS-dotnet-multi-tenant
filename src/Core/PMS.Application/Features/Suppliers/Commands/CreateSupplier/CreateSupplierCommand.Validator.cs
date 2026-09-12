using FluentValidation;

namespace PMS.Application.Features.Suppliers.Commands.CreateSupplier;

/// <summary>
/// Name and phone, and nothing else.
///
/// <para>Email and address are genuinely optional — plenty of local distributors have neither,
/// and a form that demanded them would be filled in with nonsense. A supplier with no phone
/// number, on the other hand, is of no use to somebody chasing a late delivery.</para>
/// </summary>
public sealed class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
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

        // Format-checked only when supplied, and loosely. A distributor's address may well be
        // "sales@popularpharma" on an internal mail server, and refusing it would block a real
        // record to enforce a rule the system has no stake in.
        RuleFor(c => c.Email)
            .EmailAddress().WithMessage("That does not look like an email address.")
            .When(c => !string.IsNullOrWhiteSpace(c.Email));
    }
}
