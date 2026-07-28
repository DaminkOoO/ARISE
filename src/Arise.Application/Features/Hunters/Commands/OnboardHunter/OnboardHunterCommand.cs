using Arise.Application.Common.Messaging;
using Arise.Domain.Hunters;

namespace Arise.Application.Features.Hunters.Commands.OnboardHunter;

/// <summary>
/// Éveille un Chasseur : crée son profil de progression et la narration personnalisée de
/// l'écran Éveil, à partir des objectifs qu'il a déclarés (doc mécaniques, section 4).
///
/// <para><paramref name="UserId"/> est l'identifiant du <b>compte</b> qui s'éveille, et il est
/// lu sur le jeton d'authentification, jamais sur le corps de la requête. La relation
/// compte↔profil, laissée ouverte quand cette commande a été écrite, est posée par
/// <c>User.RattacherLeProfil</c> : c'est elle qui permet ensuite à tous les endpoints protégés
/// de déduire le profil visé du jeton, plutôt que de faire confiance à l'appelant.</para>
/// </summary>
public sealed record OnboardHunterCommand(Guid UserId, IReadOnlyList<HunterGoal> Objectifs)
    : ICommand<OnboardHunterResult>;

/// <summary>
/// Le profil fraîchement créé, avec la narration d'Éveil qui l'accompagne.
///
/// <para><see cref="EstRepli"/> reflète le champ du même nom sur
/// <c>OnboardingAgentResult</c> : une narration de secours neutre plutôt qu'un texte généré,
/// pour que l'écran Éveil puisse, s'il le souhaite un jour, adapter son habillage.</para>
/// </summary>
public sealed record OnboardHunterResult(
    Guid HunterProfileId,
    int Level,
    HunterRank Rank,
    int CurrentXp,
    int XpToNextLevel,
    string AwakeningNarrative,
    bool EstRepli);
