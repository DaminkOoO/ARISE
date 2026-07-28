using Arise.Domain.Tasks;
using FluentValidation;

namespace Arise.Application.Features.Tasks.Commands.CreateTask;

/// <summary>
/// Contrôles de forme de la déclaration d'une tâche. Les messages sont écrits explicitement
/// plutôt que laissés aux gabarits par défaut : ils remontent jusqu'à l'écran (règle n°7) et
/// doivent dire quoi corriger, pas seulement que c'est refusé.
///
/// <para>Aucun contrôle sur l'échéance : une date passée est une saisie légitime, et une date
/// lointaine aussi. Le seul refus possible serait arbitraire.</para>
/// </summary>
public sealed class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        // Sans Stop, un titre vide échoue à la fois sur la présence et sur la longueur, et
        // l'écran affiche deux reproches pour une seule case à remplir. Stop garantit aussi aux
        // prédicats suivants que le titre n'est ni null ni blanc.
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(commande => commande.HunterProfileId)
            .NotEmpty()
                .WithMessage("Le profil de Chasseur ciblé est obligatoire.");

        // Les contrôles portent sur le titre rogné, car c'est celui que le handler enregistrera :
        // mesurer le brut refuserait une saisie que l'entité aurait acceptée une fois rognée.
        RuleFor(commande => commande.Title)
            .Must(titre => !string.IsNullOrWhiteSpace(titre))
                .WithMessage("Le titre de la tâche est obligatoire.")
            .Must(titre => titre.Trim().Length <= TaskItem.LongueurMaximaleTitre)
                .WithMessage(
                    "Le titre de la tâche ne peut pas dépasser "
                    + $"{TaskItem.LongueurMaximaleTitre} caractères.");
    }
}
