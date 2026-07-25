using FluentValidation;

namespace Arise.Application.Features.Sport.Commands.GenerateTodayQuest;

/// <summary>
/// Contrôles de forme de la commande de génération. Ils ne peuvent pas s'appuyer sur ceux de
/// l'appelant : la commande est envoyée par la requête de lecture aujourd'hui, par le
/// <c>briefing-worker</c> demain.
/// </summary>
public sealed class GenerateTodayQuestCommandValidator : AbstractValidator<GenerateTodayQuestCommand>
{
    public GenerateTodayQuestCommandValidator()
    {
        RuleFor(commande => commande.HunterProfileId)
            .NotEmpty()
                .WithMessage("Le profil de Chasseur ciblé est obligatoire.");

        // Une DateOnly par défaut vaut le 1er janvier de l'an 1 : elle passerait tous les
        // contrôles de type et poserait une quête que personne ne relira jamais.
        RuleFor(commande => commande.QuestDate)
            .NotEqual(default(DateOnly))
                .WithMessage("Le jour de la quête est obligatoire.");
    }
}
