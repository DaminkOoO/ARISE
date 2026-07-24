using FluentValidation;

namespace Arise.Application.Features.Hunters.Commands.OnboardHunter;

/// <summary>
/// Contrôle de forme de l'Éveil : au moins un objectif doit être déclaré, sans quoi ni l'agent
/// ni la narration n'ont de matière à personnaliser.
/// </summary>
public sealed class OnboardHunterCommandValidator : AbstractValidator<OnboardHunterCommand>
{
    public OnboardHunterCommandValidator()
    {
        RuleFor(commande => commande.Objectifs)
            .NotEmpty()
                .WithMessage("Choisis au moins un objectif pour commencer ton Éveil.");
    }
}
