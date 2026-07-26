namespace Arise.Application.Common.Exceptions;

/// <summary>
/// Le Chasseur suit déjà une habitude active portant ce nom.
///
/// <para>Le message est affichable tel quel : il est en français, au tutoiement (règle n°7), et
/// dit quoi faire ensuite sans reprocher au Chasseur de s'être répété.</para>
/// </summary>
public sealed class HabitNameAlreadyTakenException()
    : Exception("Tu suis déjà une habitude qui porte ce nom. Choisis-en un autre.");
