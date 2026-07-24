namespace Arise.Domain.Quests;

/// <summary>
/// Qualificatif de difficulté d'une quête (doc mécaniques, section 3, champ
/// <c>difficulty</c>). C'est lui qui borne la récompense d'XP — voir
/// <see cref="BaremeXpQuete"/>.
/// </summary>
public enum QuestDifficulty
{
    Facile,
    Moyenne,
    Difficile,
}
