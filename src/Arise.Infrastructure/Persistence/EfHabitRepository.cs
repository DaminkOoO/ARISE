using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Domain.Habits;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Arise.Infrastructure.Persistence;

/// <summary>
/// Accès EF Core aux habitudes déclarées.
///
/// <para><see cref="GetForHunterAsync"/> passe en <c>AsNoTracking</c> : c'est le chemin de
/// lecture de l'écran Habitudes, qui affiche et ne mute rien. La journalisation, elle, chargera
/// l'habitude par un autre chemin — suivi, celui-là.</para>
/// </summary>
internal sealed class EfHabitRepository(AriseDbContext context) : IHabitRepository
{
    /// <summary>
    /// L'égalité sur la colonne suffit à l'insensibilité à la casse : elle porte la collation
    /// non déterministe du contexte, et c'est PostgreSQL qui compare. Une normalisation en C#
    /// (<c>ToLower</c>) donnerait un résultat différent de celui de l'index unique — donc un
    /// contrôle applicatif qui laisse passer ce que la base refuse.
    /// </summary>
    public Task<bool> ExistsWithNameAsync(
        Guid hunterProfileId, string name, CancellationToken cancellationToken) =>
        context.Habits
            .AsNoTracking()
            .AnyAsync(
                habit => habit.HunterProfileId == hunterProfileId
                    && habit.Name == name
                    && !habit.IsArchived,
                cancellationToken);

    public async Task<IReadOnlyList<Habit>> GetForHunterAsync(
        Guid hunterProfileId, CancellationToken cancellationToken) =>
        await context.Habits
            .AsNoTracking()
            .Where(habit => habit.HunterProfileId == hunterProfileId)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Habit habit, CancellationToken cancellationToken)
    {
        await context.Habits.AddAsync(habit, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        // Même traduction que dans EfUserRepository : la course entre deux déclarations du même
        // nom ne se tranche pas par une lecture préalable, mais par une violation 23505. Rendue
        // dans le vocabulaire métier, elle remonte au Chasseur en français — là où une
        // DbUpdateException nue retomberait sur un 500 en anglais.
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new HabitNameAlreadyTakenException();
        }
    }
}
