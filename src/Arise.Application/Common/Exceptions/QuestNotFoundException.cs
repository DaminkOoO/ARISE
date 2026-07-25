namespace Arise.Application.Common.Exceptions;

/// <summary>
/// La quête visée n'existe pas, ou n'appartient pas au Chasseur qui la réclame — les deux cas
/// rendent volontairement la même chose, pour ne pas révéler l'existence de la quête d'autrui.
///
/// <para>Le message reste un constat, jamais un reproche (règle n°5) : le chemin le plus
/// probable pour y arriver est un écran resté ouvert la veille, pas une tentative de fraude.</para>
/// </summary>
public sealed class QuestNotFoundException()
    : Exception("Cette quête est introuvable.");
