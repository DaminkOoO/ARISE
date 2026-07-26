using Arise.Domain.Habits;
using FluentValidation;

namespace Arise.Application.Features.Habits.Commands.CreateHabit;

/// <summary>
/// Contrôles de forme de la déclaration d'une habitude. Les messages sont écrits explicitement
/// plutôt que laissés aux gabarits par défaut : ils remontent jusqu'à l'écran (règle n°7) et
/// doivent dire quoi corriger, pas seulement que c'est refusé.
///
/// <para>Pas de plafond sur le nom <b>avant</b> rognage, contrairement à l'inscription : le
/// plafond d'allocation de <c>PolitiqueIdentifiants</c> protège une route ouverte sans
/// authentification, ce que la déclaration d'une habitude n'est pas.</para>
/// </summary>
public sealed class CreateHabitCommandValidator : AbstractValidator<CreateHabitCommand>
{
    public CreateHabitCommandValidator()
    {
        // Sans Stop, un nom vide échoue à la fois sur la présence et sur la longueur, et l'écran
        // affiche deux reproches pour une seule case à remplir. Stop garantit aussi aux
        // prédicats suivants que le nom n'est ni null ni blanc.
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(commande => commande.HunterProfileId)
            .NotEmpty()
                .WithMessage("Le profil de Chasseur ciblé est obligatoire.");

        // Les contrôles portent sur le nom rogné, car c'est celui que le handler enregistrera :
        // mesurer le brut refuserait une saisie que l'entité aurait acceptée une fois rognée.
        RuleFor(commande => commande.Name)
            .Must(nom => !string.IsNullOrWhiteSpace(nom))
                .WithMessage("Le nom de l'habitude est obligatoire.")
            .Must(nom => nom.Trim().Length <= Habit.LongueurMaximaleNom)
                .WithMessage(
                    "Le nom de l'habitude ne peut pas dépasser "
                    + $"{Habit.LongueurMaximaleNom} caractères.");

        // Le rythme arrive du client en JSON : un entier hors énumération se lie sans broncher,
        // et finirait en base sous forme d'un texte que rien ne sait relire.
        RuleFor(commande => commande.Frequency)
            .IsInEnum()
                .WithMessage("Ce rythme d'habitude est inconnu.");
    }
}
