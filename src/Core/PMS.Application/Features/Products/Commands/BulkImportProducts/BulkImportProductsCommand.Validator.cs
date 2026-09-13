using FluentValidation;

namespace PMS.Application.Features.Products.Commands.BulkImportProducts;

/// <summary>
/// Only the shape of the request itself.
///
/// <para><b>The per-item rules are deliberately not here.</b> FluentValidation reports a
/// collection failure as <c>Items[14].BaseUnitName</c>, and the pipeline behaviour turns any
/// failure into a 400 with no per-row structure — so a batch of two hundred with three bad
/// rows would come back as a wall of indexed messages with nothing saying which three rows to
/// look at. The handler validates each item itself and returns a result per row, which is
/// what a review grid can actually render. See the handler.</para>
/// </summary>
public sealed class BulkImportProductsCommandValidator
    : AbstractValidator<BulkImportProductsCommand>
{
    /// <summary>
    /// The most items one request may carry.
    ///
    /// <para>Two hundred, which comfortably covers an onboarding session and keeps one request
    /// from holding a transaction open over thousands of inserts. A pharmacy with more than
    /// that does it twice; a client that sends two thousand by accident is told the number
    /// rather than left waiting.</para>
    /// </summary>
    public const int MaxItems = 200;

    public BulkImportProductsCommandValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Choose at least one medicine to import.");

        RuleFor(x => x.Items)
            .Must(items => items.Count <= MaxItems)
            .WithMessage(
                $"A single import can hold at most {MaxItems} medicines. "
                + "Split the selection and import it in more than one go.")
            .When(x => x.Items is not null);
    }
}
