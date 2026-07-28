using Arise.Application.Common.Abstractions;
using Arise.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Arise.Infrastructure.Persistence;

/// <summary>
/// Accès EF Core aux tâches ponctuelles.
///
/// <para><see cref="GetByIdAsync"/> ne passe <b>pas</b> par <c>AsNoTracking</c>, contrairement à
/// <see cref="GetForHunterAsync"/> : la complétion charge la tâche par cette voie, la mute en
/// mémoire et la re-sauvegarde dans le même scope — c'est le suivi de modifications d'EF Core qui
/// doit détecter cette mutation, pas une comparaison manuelle de champs. Même partage que dans
/// <see cref="EfQuestRepository"/>.</para>
/// </summary>
internal sealed class EfTaskItemRepository(AriseDbContext context) : ITaskItemRepository
{
    public Task<TaskItem?> GetByIdAsync(Guid taskId, CancellationToken cancellationToken) =>
        context.Tasks.SingleOrDefaultAsync(task => task.Id == taskId, cancellationToken);

    public async Task<IReadOnlyList<TaskItem>> GetForHunterAsync(
        Guid hunterProfileId, CancellationToken cancellationToken) =>
        await context.Tasks
            .AsNoTracking()
            .Where(task => task.HunterProfileId == hunterProfileId)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Borne haute <b>exclue</b> : minuit appartient au jour qui commence, pas à celui qui
    /// finit. Une borne inclusive des deux côtés compterait deux fois une tâche cochée à minuit
    /// pile, et lui vaudrait deux fois son XP.
    /// </summary>
    public Task<int> CountCompletedBetweenAsync(
        Guid hunterProfileId,
        DateTimeOffset debutInclus,
        DateTimeOffset finExclue,
        CancellationToken cancellationToken) =>
        context.Tasks
            .AsNoTracking()
            .CountAsync(
                task => task.HunterProfileId == hunterProfileId
                    && task.CompletedAt != null
                    && task.CompletedAt >= debutInclus
                    && task.CompletedAt < finExclue,
                cancellationToken);

    public async Task AddAsync(TaskItem task, CancellationToken cancellationToken)
    {
        await context.Tasks.AddAsync(task, cancellationToken);

        // Aucune violation d'unicité à traduire, contrairement aux habitudes : deux tâches
        // homonymes sont légitimes, il n'y a donc aucun index unique à heurter.
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Même logique que <see cref="EfQuestRepository.SaveAsync"/> : « nouvelle vs existante » se
    /// décide à partir de l'état de suivi, jamais d'une requête d'existence supplémentaire.
    ///
    /// <para>Aucun rattrapage de concurrence, contrairement aux quêtes : la tâche ne porte pas de
    /// jeton, parce que deux complétions simultanées écrivent le même instant à quelques
    /// millisecondes près et que rien n'est crédité deux fois. C'est l'XP qui rendait la course
    /// dangereuse sur les quêtes.</para>
    /// </summary>
    public async Task SaveAsync(TaskItem task, CancellationToken cancellationToken)
    {
        if (context.Entry(task).State == EntityState.Detached)
        {
            await context.Tasks.AddAsync(task, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
