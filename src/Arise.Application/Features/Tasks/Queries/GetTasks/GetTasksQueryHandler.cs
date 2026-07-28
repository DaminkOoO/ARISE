using Arise.Application.Common.Abstractions;
using MediatR;

namespace Arise.Application.Features.Tasks.Queries.GetTasks;

/// <summary>
/// Rend les tâches qu'il reste au Chasseur à faire.
///
/// <para>Le tri des tâches faites se décide ici et non dans le repository : c'est une règle
/// d'affichage — « ce qu'il te reste » — et la garder visible dans la couche Application la rend
/// éprouvable sans base. Le repository, lui, rend tout ce qui a été déclaré, et le prochain écran
/// qui voudra montrer l'historique des tâches cochées n'aura pas de requête à réécrire. Même
/// partage que <c>GetHabitsQuery</c> avec les archivées.</para>
///
/// <para>Aucun contrôle d'existence du profil : une lecture pour un Chasseur inconnu rend une
/// liste vide, qui est la réponse vraie. Seules les commandes refusent.</para>
/// </summary>
public sealed class GetTasksQueryHandler(ITaskItemRepository tasks)
    : IRequestHandler<GetTasksQuery, IReadOnlyList<TaskSummary>>
{
    public async Task<IReadOnlyList<TaskSummary>> Handle(
        GetTasksQuery request, CancellationToken cancellationToken)
    {
        var declarees = await tasks.GetForHunterAsync(
            request.HunterProfileId, cancellationToken);

        return declarees
            .Where(tache => !tache.IsCompleted)
            // L'échéance la plus proche d'abord, et le retard remonte donc de lui-même en tête.
            // Une tâche sans échéance n'est pas urgente : elle passe après toutes celles que le
            // Chasseur a datées, là où un tri naïf sur null les mettrait en premier.
            .OrderBy(tache => tache.DueDate is null)
            .ThenBy(tache => tache.DueDate)
            // Ordre stable : sans ce départage, la liste dépendrait de l'ordre des lignes rendu
            // par PostgreSQL et se réarrangerait d'un rafraîchissement à l'autre. Le titre
            // départage deux tâches créées au même instant, que la date seule laisserait à
            // égalité.
            .ThenBy(tache => tache.CreatedAt)
            .ThenBy(tache => tache.Title, StringComparer.CurrentCulture)
            .Select(tache => new TaskSummary(tache.Id, tache.Title, tache.DueDate))
            .ToList();
    }
}
