using Arise.Domain.Habits;

namespace Arise.Application.Common.Abstractions;

/// <summary>
/// Accès aux habitudes déclarées par les Chasseurs. Implémenté dans la couche Infrastructure :
/// la couche Application ignore qu'il y a un PostgreSQL derrière.
/// </summary>
public interface IHabitRepository
{
    /// <summary>
    /// Ce Chasseur suit-il déjà une habitude <b>active</b> portant ce nom ?
    ///
    /// <para>Deux points du contrat, et non des détails d'implémentation. La comparaison se fait
    /// <b>sans distinction de casse</b> : « Courir » et « courir » désignent la même intention,
    /// et les laisser coexister scinderait en deux la série de ce qui n'en est qu'une. Les
    /// habitudes <b>archivées</b>, en revanche, ne comptent pas : un Chasseur qui range
    /// « Courir » en janvier doit pouvoir s'y remettre en mars sans que le Système lui oppose
    /// une habitude qu'il ne voit plus nulle part.</para>
    /// </summary>
    Task<bool> ExistsWithNameAsync(
        Guid hunterProfileId, string name, CancellationToken cancellationToken);

    /// <summary>
    /// Toutes les habitudes de ce Chasseur, archivées comprises. Le tri sur celles à afficher
    /// est la décision de la requête de lecture, pas du stockage.
    /// </summary>
    Task<IReadOnlyList<Habit>> GetForHunterAsync(
        Guid hunterProfileId, CancellationToken cancellationToken);

    Task AddAsync(Habit habit, CancellationToken cancellationToken);
}
