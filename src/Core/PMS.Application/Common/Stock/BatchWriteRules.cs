using FluentValidation;

namespace PMS.Application.Common.Stock;

/// <summary>
/// The fields both batch writes carry, so the rules below are written once.
/// </summary>
/// <remarks>
/// Quantity is absent, and that absence is the design: it is set once at creation and changed
/// only by an adjustment. See <c>Batch.Adjust</c>.
/// </remarks>
public interface IBatchWriteRequest
{
    string BatchNumber { get; }

    DateOnly? ExpiryDate { get; }

    DateOnly? ManufactureDate { get; }

    string? SupplierNameText { get; }

    string? Notes { get; }
}

/// <summary>
/// Validation shared by creating and editing a batch.
///
/// <para><b>What is deliberately not here.</b> Two rules depend on the product this batch
/// belongs to — whether an expiry date is required at all, and whether the chosen unit level
/// exists — and a validator cannot see another table. Those are enforced in the handlers,
/// which have the product in hand, and returned as field errors so they land on the right
/// input. The split is worth knowing about: a rule missing from this file is not necessarily
/// a rule missing from the system.</para>
/// </summary>
public static class BatchWriteRules
{
    /// <summary>Longest batch number worth accepting. Real ones are short codes off a pack.</summary>
    public const int BatchNumberMaxLength = 100;

    public const int SupplierNameMaxLength = 200;

    public const int NotesMaxLength = 1000;

    /// <summary>
    /// How far in the future an expiry date may plausibly be. A manufacturer date typed into
    /// the expiry field is the mistake this catches — and a typo of 2206 for 2026 would
    /// otherwise sit in the data for 180 years, keeping the batch out of every expiry report
    /// while looking entirely fine on screen.
    /// </summary>
    public const int MaxYearsAhead = 30;

    public static void ApplyTo<T>(AbstractValidator<T> validator)
        where T : IBatchWriteRequest
    {
        validator.RuleFor(x => x.BatchNumber)
            .NotEmpty().WithMessage("Enter the batch number from the pack.")
            .MaximumLength(BatchNumberMaxLength)
            .WithMessage($"A batch number cannot be longer than {BatchNumberMaxLength} characters.");

        // Separate RuleFor calls, not a chain with a trailing .When(). A trailing When applies
        // to the whole chain, which would switch off the NotEmpty above along with it — the
        // exact bug that let a mid unit through without its pack size in Module 2.
        validator.RuleFor(x => x.ManufactureDate)
            .LessThan(x => x.ExpiryDate)
            .WithMessage("The manufacture date has to be before the expiry date.")
            .When(x => x.ManufactureDate is not null && x.ExpiryDate is not null);

        validator.RuleFor(x => x.ExpiryDate)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow).AddYears(MaxYearsAhead))
            .WithMessage($"That expiry date is more than {MaxYearsAhead} years away — please check it.")
            .When(x => x.ExpiryDate is not null);

        validator.RuleFor(x => x.SupplierNameText)
            .MaximumLength(SupplierNameMaxLength)
            .WithMessage($"A supplier name cannot be longer than {SupplierNameMaxLength} characters.");

        validator.RuleFor(x => x.Notes)
            .MaximumLength(NotesMaxLength)
            .WithMessage($"Notes cannot be longer than {NotesMaxLength} characters.");
    }
}
