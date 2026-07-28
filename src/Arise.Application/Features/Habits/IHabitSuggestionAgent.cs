using Arise.Application.Common.Abstractions;
using Arise.Domain.Habits;
using Arise.Domain.Hunters;

namespace Arise.Application.Features.Habits;

/// <summary>
/// L'agent qui propose au Chasseur des habitudes à se donner. Il rend des suggestions <b>déjà
/// validées</b> : noms dans les bornes de <see cref="Habit.LongueurMaximaleNom"/>, rythmes dans
/// l'énumération attendue, texte conforme aux garde-fous — aucune prescription de santé ou de
/// nutrition, aucun diagnostic, aucun reproche, et du français.
///
/// <para>Ce qui ne passe pas ces contrôles ne remonte jamais jusqu'ici : l'implémentation
/// réessaie une fois, puis se replie sur une liste générique sûre.</para>
/// </summary>
public interface IHabitSuggestionAgent
    : IAgent<HabitSuggestionAgentRequest, HabitSuggestionAgentResult>
{
}

/// <summary>
/// Contexte transmis au Système pour personnaliser les suggestions.
/// </summary>
/// <param name="HabitudesExistantes">
/// Les habitudes <b>actives</b> du Chasseur, pour que le Système ne repropose pas ce qu'il suit
/// déjà — la suggestion la plus inutile qui soit. Les archivées n'y figurent pas : les
/// reproposer est justement le service attendu.
/// </param>
public sealed record HabitSuggestionAgentRequest(
    int Level,
    HunterRank Rank,
    IReadOnlyList<string> HabitudesExistantes);

/// <summary>
/// Les habitudes proposées.
///
/// <para><see cref="EstRepli"/> distingue une liste réellement générée d'une liste générique de
/// secours — même convention que <c>QuestGenerationAgentResult</c>.</para>
/// </summary>
public sealed record HabitSuggestionAgentResult(
    IReadOnlyList<HabitSuggestion> Suggestions,
    bool EstRepli);

/// <summary>
/// Une habitude proposée, prête à être déclarée par <c>CreateHabitCommand</c> si le Chasseur la
/// retient.
/// </summary>
public sealed record HabitSuggestion(string Name, HabitFrequency Frequency);
