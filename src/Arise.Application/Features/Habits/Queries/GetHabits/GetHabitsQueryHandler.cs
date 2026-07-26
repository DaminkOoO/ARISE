using Arise.Application.Common.Abstractions;
using MediatR;

namespace Arise.Application.Features.Habits.Queries.GetHabits;

/// <summary>
/// Rend les habitudes que le Chasseur suit aujourd'hui.
///
/// <para>Le tri des archivées se décide ici et non dans le repository : c'est une règle
/// d'affichage — « ce que le Chasseur suit encore » — et la garder visible dans la couche
/// Application la rend éprouvable sans base. Le repository, lui, rend tout ce qui a été
/// déclaré, et le prochain écran qui voudra montrer les habitudes rangées n'aura pas de requête
/// à réécrire.</para>
///
/// <para>Aucun contrôle d'existence du profil : une lecture pour un Chasseur inconnu rend une
/// liste vide, qui est la réponse vraie. Seules les commandes refusent — c'est déjà le partage
/// retenu entre <c>GetTodayGymQuestQuery</c> et <c>GenerateTodayQuestCommand</c>.</para>
/// </summary>
public sealed class GetHabitsQueryHandler(IHabitRepository habits)
    : IRequestHandler<GetHabitsQuery, IReadOnlyList<HabitSummary>>
{
    public async Task<IReadOnlyList<HabitSummary>> Handle(
        GetHabitsQuery request, CancellationToken cancellationToken)
    {
        var declarees = await habits.GetForHunterAsync(
            request.HunterProfileId, cancellationToken);

        return declarees
            .Where(habitude => !habitude.IsArchived)
            // Ordre stable : sans lui, la liste dépendrait de l'ordre des lignes rendu par
            // PostgreSQL et se réarrangerait d'un rafraîchissement à l'autre. Le nom départage
            // deux déclarations du même instant, que la date seule laisserait à égalité.
            .OrderBy(habitude => habitude.CreatedAt)
            .ThenBy(habitude => habitude.Name, StringComparer.CurrentCulture)
            .Select(habitude => new HabitSummary(
                habitude.Id, habitude.Name, habitude.Frequency))
            .ToList();
    }
}
