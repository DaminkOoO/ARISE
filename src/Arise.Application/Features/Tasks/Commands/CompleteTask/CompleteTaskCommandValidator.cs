using Arise.Application.Common.Validation;
using FluentValidation;

namespace Arise.Application.Features.Tasks.Commands.CompleteTask;

/// <summary>
/// Contrôles de forme de la complétion d'une tâche. Le fuseau y est décisif non pour dater la
/// complétion — un instant absolu suffirait — mais parce que le plafond quotidien d'XP
/// d'engagement se recompte sur la journée du Chasseur.
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

        RuleFor(commande => commande.FuseauHoraire).FuseauHoraireDuChasseur();
    }
}
