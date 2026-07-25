namespace Arise.Application.Common.Exceptions;

/// <summary>
/// La quête a été écrite par quelqu'un d'autre entre sa lecture et sa sauvegarde : le jeton de
/// concurrence vient de le trancher au moment de l'écriture.
///
/// <para>Le chemin le plus probable est le double-tap <b>simultané</b> — deux requêtes, deux
/// scopes, deux <c>DbContext</c> qui lisent tous deux une quête non complétée. La garde
/// d'idempotence de l'entité ne peut rien y voir : elle est en mémoire, et chacun a la sienne.
/// </para>
///
/// <para>Comme <see cref="QuestAlreadyPosedException"/>, c'est un signal destiné au chemin
/// d'écriture — qui repart de l'état gagnant, rafraîchi par le repository — et non une erreur
/// que le Chasseur doive lire. Le message reste néanmoins en français (règle n°7).</para>
/// </summary>
public sealed class ConcurrentQuestUpdateException()
    : Exception("Cette quête vient d'être modifiée par ailleurs.");
