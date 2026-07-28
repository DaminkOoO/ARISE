using Arise.Domain.Habits;

namespace Arise.Domain.Hunters;

/// <summary>
/// XP accordé par les gestes d'<b>engagement</b> — tenir une habitude, cocher une tâche
/// (doc mécaniques, section 1). Seule et unique définition de ces montants dans le dépôt.
///
/// <para>Distinct de <see cref="Quests.BaremeXpQuete"/> sur un point qui compte : ces montants
/// sont <b>fixes</b>. Ils ne sortent d'aucun agent, et il n'y a donc aucune réponse de modèle à
/// valider — là où une récompense de quête est proposée par Gemini et doit tomber dans une
/// fourchette.</para>
///
/// <para>Volontairement petits face aux 60–100 XP/jour des quêtes : le rythme annoncé par le
/// document — rang S en ~3 mois — est calculé sur les seules quêtes, et tout ce qu'on ajoute à
/// côté raccourcit ce délai. L'engagement est une garniture, pas une seconde source.</para>
/// </summary>
public static class BaremeXpEngagement
{
    /// <summary>
    /// Plafond d'XP d'engagement accordable au Chasseur sur <b>sa</b> journée, habitudes et
    /// tâches confondues.
    ///
    /// <para>C'est la seule protection contre la ferme d'XP, et elle est indispensable : le
    /// nombre de quêtes du jour est fixé par le Système, mais le nombre d'habitudes et de tâches
    /// est fixé par le Chasseur lui-même. Sans plafond, déclarer cinquante tâches et les cocher
    /// d'affilée vaut 250 XP en une minute.</para>
    ///
    /// <para><b>Cumulé</b> entre les deux domaines et non séparé par domaine : deux plafonds
    /// séparés se contournent en alternant.</para>
    /// </summary>
    public const int PlafondQuotidien = 25;

    /// <summary>
    /// Une tâche cochée. Au-dessus d'une habitude quotidienne : c'est un effort ponctuel et
    /// souvent plus gros, là où une habitude est une petite brique répétée.
    /// </summary>
    public const int PourTache = 5;

    /// <summary>
    /// Une habitude tenue, selon son rythme.
    ///
    /// <para>L'hebdomadaire vaut plus que la quotidienne parce qu'elle est un engagement
    /// <b>unique</b> et non sept petits : la payer au même tarif rendrait le rythme hebdomadaire
    /// strictement perdant, et personne ne le choisirait.</para>
    /// </summary>
    public static int PourHabitude(HabitFrequency frequence) => frequence switch
    {
        HabitFrequency.Quotidienne => 3,
        HabitFrequency.Hebdomadaire => 10,
        _ => throw new ArgumentOutOfRangeException(nameof(frequence)),
    };

    /// <summary>
    /// L'XP réellement accordable pour un geste valant <paramref name="valeurDuGeste"/>, sachant
    /// que <paramref name="dejaAcquisAujourdHui"/> a déjà été accordé sur la journée du Chasseur.
    ///
    /// <para>Le geste n'est jamais <b>refusé</b> par le plafond : l'habitude est tenue, la tâche
    /// est faite. Seul le gain est rogné — ce qui permet à l'écran d'annoncer « plafond atteint »
    /// plutôt que de faire échouer un geste parfaitement légitime.</para>
    ///
    /// <para>Borné à zéro par le bas : le total du jour étant recalculé et non stocké, un
    /// décompte qui dépasserait le plafond ne doit surtout pas produire un gain négatif, qui
    /// <i>retirerait</i> de l'XP au Chasseur.</para>
    /// </summary>
    public static int Accordable(int valeurDuGeste, int dejaAcquisAujourdHui) =>
        Math.Clamp(PlafondQuotidien - dejaAcquisAujourdHui, 0, valeurDuGeste);

    /// <summary>
    /// L'XP d'engagement déjà acquis sur la journée du Chasseur, <b>recalculé</b> depuis ses
    /// gestes du jour — jamais lu sur un compteur.
    ///
    /// <para>C'est la même discipline que la série d'une habitude, et pour la même raison : un
    /// total entretenu à côté divergerait de ses gestes à la première écriture concurrente, et
    /// plus rien ne dirait lequel des deux a raison.</para>
    /// </summary>
    public static int TotalDuJour(
        IEnumerable<HabitFrequency> habitudesTenues, int tachesCompletees) =>
        habitudesTenues.Sum(PourHabitude) + (tachesCompletees * PourTache);
}
