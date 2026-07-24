using Arise.Application.Common.Abstractions;
using Arise.Domain.Hunters;
using Arise.Domain.Quests;

namespace Arise.Application.Features.Sport;

/// <summary>
/// L'agent qui écrit la quête de sport du jour (doc mécaniques, section 3). Il rend une quête
/// <b>déjà validée</b> : type et statistique dans les énumérations attendues, récompense dans
/// la fourchette de sa difficulté (<see cref="BaremeXpQuete"/>), texte conforme aux garde-fous
/// du sport. Ce qui ne passe pas ces contrôles ne remonte jamais jusqu'ici — l'implémentation
/// réessaie une fois, puis se replie sur une quête générique sûre.
/// </summary>
public interface IQuestGenerationAgent
    : IAgent<QuestGenerationAgentRequest, QuestGenerationAgentResult>
{
}

/// <summary>
/// Contexte transmis au Système pour personnaliser la quête (doc mécaniques, section 3).
///
/// <para>Volontairement limité à ce que le dépôt sait réellement du Chasseur aujourd'hui :
/// niveau, rang et série en cours. Les objectifs déclarés à l'onboarding ne sont persistés
/// nulle part, les cinq statistiques ne sont pas encore portées par
/// <see cref="HunterProfile"/>, et l'historique de complétion sur sept jours n'existe pas —
/// les inventer ici reviendrait à passer au modèle des données fabriquées.</para>
/// </summary>
public sealed record QuestGenerationAgentRequest(int Level, HunterRank Rank, int StreakCurrent);

/// <summary>
/// La quête générée, prête à devenir un <see cref="Quest"/>.
///
/// <para><see cref="EstRepli"/> distingue une quête réellement générée d'une quête générique
/// de secours — même convention que <c>OnboardingAgentResult</c> : chaque DTO de résultat
/// concret porte son propre champ plutôt qu'un wrapper générique.</para>
/// </summary>
public sealed record QuestGenerationAgentResult(
    string Title,
    string Description,
    QuestType Type,
    QuestStat StatTarget,
    QuestDifficulty Difficulty,
    int XpReward,
    bool EstRepli);
