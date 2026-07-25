namespace Arise.Application.Common.Exceptions;

/// <summary>
/// Une quête existe déjà pour ce Chasseur, ce domaine et ce jour : l'index unique vient de le
/// trancher au moment de l'écriture.
///
/// <para>Ce n'est pas une erreur que le Chasseur doive lire — deux appareils, ou un
/// rafraîchissement pendant l'appel au Système, suffisent à la provoquer — mais un signal
/// destiné au chemin d'écriture, qui relit alors la quête gagnante et la rend. Le message reste
/// néanmoins en français (règle n°7) : rien ne garantit qu'aucun chemin ne la laissera jamais
/// remonter.</para>
/// </summary>
public sealed class QuestAlreadyPosedException()
    : Exception("Une quête est déjà posée pour ce Chasseur, ce domaine et ce jour.");
