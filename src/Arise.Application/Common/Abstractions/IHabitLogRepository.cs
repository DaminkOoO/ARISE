using Arise.Domain.Habits;

namespace Arise.Application.Common.Abstractions;

/// <summary>
/// Accès au journal des habitudes tenues. Implémenté dans la couche Infrastructure : la couche
/// Application ignore qu'il y a un PostgreSQL derrière.
/// </summary>
public interface IHabitLogRepository
{
    /// <summary>
    /// Les jours — au sens du fuseau du Chasseur — où cette habitude a été tenue.
    ///
    /// <para>Rend des <see cref="DateOnly"/> et non des <see cref="HabitLog"/> : la série est la
    /// seule chose qu'on en déduise, et elle ne connaît que des jours. Projeter au plus juste
    /// garde la lecture petite même sur une habitude tenue depuis deux ans, là où charger les
    /// entités ramènerait identifiants et horodatages dont personne n'a l'usage.</para>
    ///
    /// <para>Aucun ordre garanti : <see cref="SerieDHabitude"/> n'en présuppose aucun, et exiger
    /// un tri ici ferait dépendre la justesse d'une série d'un <c>ORDER BY</c>.</para>
    /// </summary>
    Task<IReadOnlyList<DateOnly>> GetDaysAsync(Guid habitId, CancellationToken cancellationToken);

    /// <summary>
    /// Ajoute une ligne au journal.
    /// </summary>
    /// <exception cref="Exceptions.HabitAlreadyLoggedException">
    /// Ce jour était déjà journalisé pour cette habitude — course entre deux taps simultanés que
    /// seule la contrainte d'unicité peut trancher.
    /// </exception>
    Task AddAsync(HabitLog log, CancellationToken cancellationToken);
}
