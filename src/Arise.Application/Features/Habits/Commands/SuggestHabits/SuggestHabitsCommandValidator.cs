using FluentValidation;

namespace Arise.Application.Features.Habits.Commands.SuggestHabits;

public sealed class SuggestHabitsCommandValidator : AbstractValidator<SuggestHabitsCommand>
{
    public SuggestHabitsCommandValidator()
    {
        RuleFor(commande => commande.HunterProfileId)
            .NotEmpty()
                .WithMessage("Le profil de Chasseur ciblé est obligatoire.");
    }
}
