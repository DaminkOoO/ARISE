using Arise.Application.Common.Validation;
using FluentValidation;

namespace Arise.Application.Features.Sport.Queries.GetTodayGymQuest;

/// <summary>
/// Contrôles de forme de la demande de quête du jour. Le fuseau est le seul champ dont la
/// validité conditionne un calcul : sans ce contrôle, un identifiant inconnu ferait lever le
/// handler avec une <see cref="TimeZoneNotFoundException"/> — une erreur technique, en
/// anglais, que le Chasseur n'a pas à lire.
///
/// <para>Le fuseau voyage dans la requête tant que <c>HunterProfile</c> ne le porte pas.
/// L'écran de profil de l'onboarding le demande pourtant (doc mécaniques, section 4) : le jour
/// où il sera persisté, ce champ disparaîtra de la requête et ce contrôle avec lui.</para>
/// </summary>
public sealed class GetTodayGymQuestQueryValidator : AbstractValidator<GetTodayGymQuestQuery>
{
    public GetTodayGymQuestQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(requete => requete.HunterProfileId)
            .NotEmpty()
                .WithMessage("Le profil de Chasseur ciblé est obligatoire.");

        RuleFor(requete => requete.FuseauHoraire).FuseauHoraireDuChasseur();
    }
}
