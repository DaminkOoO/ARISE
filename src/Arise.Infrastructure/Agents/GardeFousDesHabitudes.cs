using System.Text.RegularExpressions;

namespace Arise.Infrastructure.Agents;

/// <summary>
/// Les garde-fous propres à une habitude suggérée, posés sur le socle commun de
/// <see cref="GardeFousTextuels"/>.
///
/// <para>Une habitude est plus exigeante qu'une quête sur un point précis : elle est destinée à
/// être <b>répétée tous les jours</b>. « Cours dix kilomètres » posé un jour est une quête
/// discutable ; « Courir dix kilomètres » posé comme habitude devient un programme
/// d'entraînement que personne n'a prescrit. Les prescriptions chiffrées y sont donc refusées
/// exactement comme dans le sport, et pour une raison plus forte encore.</para>
///
/// <para>Extraits ici plutôt que gardés privés dans l'agent : les tests de l'agent s'en servent
/// pour vérifier que <b>son propre repli</b> passe les contrôles qu'il exige du modèle. Un repli
/// qu'aucun test ne repasse au tamis est un texte utilisateur non validé.</para>
/// </summary>
internal static class GardeFousDesHabitudes
{
    /// <summary>
    /// Toute mention d'une unité de distance, <b>chiffre ou pas</b>.
    ///
    /// <para>C'est là que le lexique du sport ne suffit pas : ses filtres de distance exigent un
    /// <c>\d+</c>, et « Courir dix kilomètres » — écrit en toutes lettres — les franchit
    /// intégralement. Sur une quête, le plafond de magnitude rattrapait le cas ; un nom
    /// d'habitude, lui, n'a pas de description où le chiffre réapparaîtrait.</para>
    ///
    /// <para>Sans plafond ni exception, donc, et c'est assumé : une habitude qui nomme une
    /// distance est un programme d'entraînement, quelle que soit la distance. « Marcher après le
    /// déjeuner » dit la même intention sans fixer à personne ce qu'il doit tenir.</para>
    /// </summary>
    private static readonly Regex DistanceMentionnee = new(
        @"\b(?:km|kilometres?|metres?|miles?|bornes?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Le motif de rejet de ce nom d'habitude, ou <see langword="null"/> s'il passe les
    /// garde-fous. Le motif est en français : il est rappelé tel quel au modèle lors de l'unique
    /// nouvelle tentative.
    /// </summary>
    public static string? Violation(string nom)
    {
        // Sur le texte brut : c'est un contrôle de langue, pas de lexique normalisé. Un nom
        // d'habitude est trop court pour qu'on y cherche des mots-outils français — on refuse
        // donc ce qui est visiblement anglais, comme pour un titre de quête.
        if (GardeFousTextuels.EstVisiblementAnglais(nom))
        {
            return "les habitudes doivent être écrites en français, au tutoiement";
        }

        var normalise = GardeFousTextuels.SansAccents(nom);

        if (GardeFousTextuels.PrescritDesChiffres(normalise))
        {
            return "une habitude ne prescrit ni charge, ni allure, ni calories, ni jeûne, ni "
                + "dosage : le Chasseur juge seul de son intensité";
        }

        if (DistanceMentionnee.IsMatch(normalise))
        {
            return "une habitude ne fixe aucune distance : « marcher après le déjeuner » dit "
                + "l'intention sans imposer ce qu'il faut tenir";
        }

        if (GardeFousTextuels.InviteAPasserOutreLaDouleur(normalise))
        {
            return "une habitude n'invite jamais à poursuivre malgré une douleur";
        }

        if (GardeFousTextuels.Reproche(normalise))
        {
            return "une habitude ne reproche jamais rien au Chasseur : elle décrit ce qu'il "
                + "choisit de faire, jamais ce qu'il aurait manqué";
        }

        if (GardeFousTextuels.EmploieUnVocabulaireInterdit(normalise))
        {
            return "une habitude ne pose aucun diagnostic, ne prescrit aucun traitement, aucun "
                + "régime, et ne formule jamais de reproche";
        }

        return null;
    }
}
