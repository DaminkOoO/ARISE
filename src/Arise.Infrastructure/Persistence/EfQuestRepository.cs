using Arise.Application.Common.Abstractions;
using Arise.Domain.Quests;
using Microsoft.EntityFrameworkCore;

namespace Arise.Infrastructure.Persistence;

/// <summary>
/// Accès EF Core aux quêtes posées. Comme <see cref="EfHunterProfileRepository"/> et
/// contrairement à <see cref="EfUserRepository"/>, <see cref="GetForDayAsync"/> ne passe pas
/// par <c>AsNoTracking</c> : la complétion d'une quête chargera la ligne, la mutera en mémoire
/// et la re-sauvegardera dans le même scope — c'est le suivi de modifications d'EF Core qui
/// doit détecter ces mutations, pas une comparaison manuelle de champs.
/// </summary>
internal sealed class EfQuestRepository(AriseDbContext context) : IQuestRepository
{
    public Task<Quest?> GetForDayAsync(
        Guid hunterProfileId,
        QuestDomain domain,
        DateOnly questDate,
        CancellationToken cancellationToken) =>
        // SingleOrDefault et non FirstOrDefault : l'index unique promet au plus une ligne, et
        // si cette promesse cassait un jour, mieux vaut le savoir que rendre une quête au
        // hasard entre deux.
        context.Quests.SingleOrDefaultAsync(
            quest => quest.HunterProfileId == hunterProfileId
                && quest.Domain == domain
                && quest.QuestDate == questDate,
            cancellationToken);

    // Même logique que EfHunterProfileRepository : « nouvelle vs existante » se décide à
    // partir de l'état de suivi, jamais d'une requête d'existence supplémentaire.
    public async Task SaveAsync(Quest quest, CancellationToken cancellationToken)
    {
        if (context.Entry(quest).State == EntityState.Detached)
        {
            await context.Quests.AddAsync(quest, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
