namespace Arise.Application.Common.Exceptions;

/// <summary>
/// L'habitude visée a été rangée : elle ne se journalise plus.
///
/// <para>Le message dit quoi faire ensuite plutôt que de reprocher le geste (règle n°5) — ranger
/// une habitude n'est pas un échec, et la retrouver dans un écran resté ouvert n'en est pas un
/// non plus.</para>
/// </summary>
public sealed class HabitArchivedException()
    : Exception("Cette habitude est rangée. Remets-la dans ta liste pour la suivre à nouveau.");
