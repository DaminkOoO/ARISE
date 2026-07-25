using Arise.Application.Common.Messaging;
using Arise.Domain.Hunters;

namespace Arise.Application.Features.Hunters.Commands.OnboardHunter;

/// <summary>
/// Éveille un Chasseur : crée son profil de progression et la narration personnalisée de
/// l'écran Éveil, à partir des objectifs qu'il a déclarés (doc mécaniques, section 4).
///
/// <para>Ne porte pas d'identifiant de compte (<c>User</c>) : ni <see cref="HunterProfile"/>
/// ni <c>User</c> ne portent aujourd'hui de schéma de relation compte↔profil dans ce dépôt, et
/// en inventer un ici serait deviner un schéma non spécifié. La liaison revient à la tâche qui
/// exposera l'endpoint — hors périmètre de celle-ci.</para>
/// </summary>
public sealed record OnboardHunterCommand(IReadOnlyList<HunterGoal> Objectifs)
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
