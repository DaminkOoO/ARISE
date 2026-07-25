using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Domain.Quests;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        // Même traduction que dans EfUserRepository : la course entre deux générations du même
        // jour ne se tranche pas par une lecture préalable, mais ici, par une violation 23505.
        // Rendue dans le vocabulaire métier, elle est rattrapable par le handler d'écriture sans
        // que la couche Application ait à connaître Npgsql — là où une DbUpdateException nue
        // retomberait sur un 500 en anglais devant le Chasseur.
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new QuestAlreadyPosedException();
        }
    }
}
