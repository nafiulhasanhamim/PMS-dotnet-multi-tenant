using FluentValidation;

namespace PMS.Application.Features.Suppliers.Commands.SetSupplierStatus;

/// <summary>
/// There is only one thing to check, and the architecture tests are right to insist it exists.
///
/// <para>The command carries an id and a boolean; the boolean cannot be invalid and the id's
/// <em>existence</em> is the handler's business, since only a database can answer it. What is
/// left is that an id was supplied at all — which a route-bound <c>Guid</c> makes unlikely and
/// not impossible, and an empty one would otherwise reach the repository and come back as a
/// confusing "not found".</para>
///
/// <para>A validator with one rule is worth more than an exemption from the convention:
/// <c>EachCommand_Should_HaveValidator</c> exists so that the next command with real rules
/// cannot quietly ship without them, and every exception carved into it makes it weaker.</para>
/// </summary>
public sealed class SetSupplierStatusCommandValidator
    : AbstractValidator<SetSupplierStatusCommand>
{
    public SetSupplierStatusCommandValidator()
    {
        RuleFor(c => c.SupplierId)
            .NotEmpty().WithMessage("Which supplier?");
    }
}
