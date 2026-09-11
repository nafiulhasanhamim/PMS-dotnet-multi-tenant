using FluentValidation;

namespace PMS.Application.Features.Settings.Commands.SetAntibioticMode;

public sealed class SetAntibioticModeCommandValidator
    : AbstractValidator<SetAntibioticModeCommand>
{
    public SetAntibioticModeCommandValidator()
    {
        // An out-of-range int arriving as an enum is the one thing worth refusing here. The
        // database carries the same check, and a mode of 7 would make every conditional in
        // Module 5 fall through to its safest arm silently.
        RuleFor(x => x.Mode)
            .IsInEnum().WithMessage("Choose one of the three prescription modes.");
    }
}
