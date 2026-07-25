using Arise.Application.Common.Validation;
using FluentValidation;

namespace Arise.Application.Features.Sport.Commands.CompleteGymQuest;

/// <summary>
/// Contrôles de forme de la complétion. Le fuseau est le seul champ dont la validité conditionne
/// un calcul : sans ce contrôle, un identifiant inconnu ferait lever le handler avec une
/// <see cref="TimeZoneNotFoundException"/> — une erreur technique, en anglais, que le Chasseur
/// n'a pas à lire.
/// </summary>
public sealed class CompleteGymQuestCommandValidator : AbstractValidator<CompleteGymQuestCommand>
{
    public CompleteGymQuestCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(commande => commande.HunterProfileId)
            .NotEmpty()
                .WithMessage("Le profil de Chasseur ciblé est obligatoire.");

        RuleFor(commande => commande.QuestId)
            .NotEmpty()
                .WithMessage("La quête à accomplir est obligatoire.");

        RuleFor(commande => commande.FuseauHoraire).FuseauHoraireDuChasseur();
    }
}
