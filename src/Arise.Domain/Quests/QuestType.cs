namespace Arise.Domain.Quests;

/// <summary>
/// Nature d'une quête (doc mécaniques, section 3, champ <c>type</c> du contrat de sortie).
///
/// <para><see cref="Penalite"/> n'est pas une punition : c'est une quête volontairement facile,
/// proposée après une série rompue pour redonner un point d'appui — elle vaut toujours 10 XP,
/// par conception.</para>
/// </summary>
public enum QuestType
{
    Quotidienne,
    Penalite,
}
