using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Domain.Habits;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Arise.Infrastructure.Persistence;

/// <summary>
/// Accès EF Core au journal des habitudes.
///
/// <para>Rien n'est jamais suivi ici : une entrée de journal ne se modifie pas — elle est écrite
/// une fois, et relue en projection. Le suivi de modifications n'aurait donc rien à détecter.
/// </para>
/// </summary>
internal sealed class EfHabitLogRepository(AriseDbContext context) : IHabitLogRepository
{
    /// <summary>
    /// Projection en base et non en mémoire : le <c>Select</c> précède la matérialisation, si
    /// bien que PostgreSQL ne rend que la colonne <c>day</c>. C'est ce qui garde la lecture
    /// petite sur une habitude tenue depuis deux ans.
    /// </summary>
    public async Task<IReadOnlyList<DateOnly>> GetDaysAsync(
        Guid habitId, CancellationToken cancellationToken) =>
        await context.HabitLogs
            .AsNoTracking()
            .Where(log => log.HabitId == habitId)
            .Select(log => log.Day)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Jointure sur <c>habits</c> pour ne retenir que les habitudes du Chasseur et lire leur
    /// rythme — l'entrée de journal ne le porte pas, et le dupliquer sur la ligne le figerait au
    /// jour de l'écriture.
    /// </summary>
    public async Task<IReadOnlyList<HabitFrequency>> GetDayFrequenciesForHunterAsync(
        Guid hunterProfileId, DateOnly jour, CancellationToken cancellationToken) =>
        await context.HabitLogs
            .AsNoTracking()
            .Where(log => log.Day == jour)
            .Join(
                context.Habits.Where(habit => habit.HunterProfileId == hunterProfileId),
                log => log.HabitId,
                habit => habit.Id,
                (_, habit) => habit.Frequency)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(HabitLog log, CancellationToken cancellationToken)
    {
        await context.HabitLogs.AddAsync(log, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        // Même traduction que dans EfHabitRepository : la course entre deux taps simultanés ne se
        // tranche pas par une lecture préalable, mais par une violation 23505. Rendue dans le
        // vocabulaire métier, elle est rattrapable par le handler — qui la traite comme un
        // double-tap — sans que la couche Application ait à connaître Npgsql.
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // L'entrée refusée reste sinon suivie en Added, et la prochaine sauvegarde du même
            // scope rejouerait son insertion — donc la même violation, cette fois loin d'ici. Le
            // handler, lui, poursuit après ce refus : la transaction reste ouverte, et ce qu'elle
            // porte encore doit être exactement ce que le Chasseur a demandé.
            context.Entry(log).State = EntityState.Detached;

            throw new HabitAlreadyLoggedException();
        }
    }
}
