using Arise.Application.Common.Messaging;
using Arise.Domain.Habits;

namespace Arise.Application.Features.Habits.Queries.GetHabits;

/// <summary>
/// Les habitudes que le Chasseur suit aujourd'hui.
/// </summary>
public sealed record GetHabitsQuery(Guid HunterProfileId)
    : IQuery<IReadOnlyList<HabitSummary>>;

/// <summary>
/// Une ligne de la liste d'habitudes, telle que l'écran l'affiche.
///
/// <para>Pas de série ici : elle se calculera depuis <c>HabitLog</c> (doc mécaniques,
/// section 2), qui n'existe pas encore. L'annoncer à zéro d'ici là afficherait un chiffre faux
/// à côté d'une flamme éteinte, ce qu'aucun Chasseur n'a mérité de lire.</para>
/// </summary>
public sealed record HabitSummary(Guid HabitId, string Name, HabitFrequency Frequency);
