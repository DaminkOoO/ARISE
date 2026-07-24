namespace Arise.Domain.Quests;

/// <summary>
/// Statistique du Chasseur qu'une quête fait progresser (doc mécaniques, section 3, champ
/// <c>stat_target</c>). Les noms sont écrits en toutes lettres ici ; les abréviations
/// FOR/VIT/INT/OR/PER du HUD comme les jetons du contrat JSON restent à la frontière qui les
/// utilise — l'agent pour le JSON, l'interface pour l'affichage.
/// </summary>
public enum QuestStat
{
    Force,
    Vitesse,
    Intelligence,
    Or,
    Perception,
}
