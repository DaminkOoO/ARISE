using FluentValidation;

namespace Arise.Application.Features.Tasks.Commands.CompleteTask;

/// <summary>
/// Contrôles de forme de la complétion d'une tâche. Pas de fuseau horaire à valider,
/// contrairement à celle d'une quête : aucun calcul ici ne dépend du jour du Chasseur.
/// </summary>
public sealed class CompleteTaskCommandValidator : AbstractValidator<CompleteTaskCommand>
{
    public CompleteTaskCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(commande => commande.HunterProfileId)
            .NotEmpty()
                .WithMessage("Le profil de Chasseur ciblé est obligatoire.");

        RuleFor(commande => commande.TaskId)
            .NotEmpty()
                .WithMessage("La tâche à cocher est obligatoire.");
    }
}
