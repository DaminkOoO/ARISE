using Arise.Application.Common.Validation;
using FluentValidation;

namespace Arise.Application.Features.Habits.Commands.LogHabit;

/// <summary>
/// Contrôles de forme de la journalisation. Le fuseau est le seul champ dont la validité
/// conditionne un calcul : sans ce contrôle, un identifiant inconnu ferait lever le handler avec
/// une <see cref="TimeZoneNotFoundException"/> — une erreur technique, en anglais, que le
/// Chasseur n'a pas à lire.
/// </summary>
public sealed class LogHabitCommandValidator : AbstractValidator<LogHabitCommand>
{
    public LogHabitCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(commande => commande.HunterProfileId)
            .NotEmpty()
                .WithMessage("Le profil de Chasseur ciblé est obligatoire.");

        RuleFor(commande => commande.HabitId)
            .NotEmpty()
                .WithMessage("L'habitude à journaliser est obligatoire.");

        RuleFor(commande => commande.FuseauHoraire).FuseauHoraireDuChasseur();
    }
}
