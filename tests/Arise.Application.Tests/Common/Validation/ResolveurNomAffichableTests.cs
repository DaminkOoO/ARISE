using System.Linq.Expressions;
using System.Reflection;
using System.Resources;
using Arise.Application.Common.Validation;
using FluentAssertions;
// Alias : le namespace DataAnnotations expose lui aussi une ValidationException, qui
// entrerait en collision avec celle de FluentValidation.
using DisplayAttribute = System.ComponentModel.DataAnnotations.DisplayAttribute;

namespace Arise.Application.Tests.Common.Validation;

/// <summary>
/// Le nommage des propriétés dans les messages de validation est éprouvé ici comme une
/// fonction pure, et non à travers <c>ValidatorOptions.Global</c>.
///
/// <para>Ce statique est global au processus : xUnit parallélisant les classes de tests d'une
/// même assembly, une classe qui n'appelle jamais <c>AddApplication()</c> observe malgré tout
/// le résolveur dès qu'une autre a gagné la course. Un test qui s'appuierait dessus
/// deviendrait intermittent, et le test fautif se trouverait dans un autre fichier.</para>
/// </summary>
public class ResolveurNomAffichableTests
{
    private sealed class Requete
    {
        [Display(Name = "nom du Chasseur")]
        public string NomDuChasseur { get; init; } = "";

        public string SansEtiquette { get; init; } = "";
    }

    [Fact]
    public void Renvoie_l_identifiant_brut_d_une_propriete_sans_etiquette()
    {
        // Le découpage PascalCase par défaut fabrique du franglais présentable
        // ('Nom Du Chasseur', 'Email Address') qui passe la revue sans se faire remarquer.
        // Laisser l'identifiant brut rend l'étiquette manquante visible.
        Resoudre((Requete r) => r.SansEtiquette).Should().Be("SansEtiquette");
    }

    [Fact]
    public void Prefere_l_etiquette_Display_a_l_identifiant()
    {
        Resoudre((Requete r) => r.NomDuChasseur).Should().Be("nom du Chasseur");
    }

    private sealed class Depense
    {
        public decimal Montant { get; init; }
    }

    private sealed class Budget
    {
        [Display(Name = "budget")]
        public Depense Plafond { get; init; } = new();
    }

    private sealed class RequeteImbriquee
    {
        public Depense Depense { get; init; } = new();

        public Budget Budget { get; init; } = new();
    }

    [Fact]
    public void Compose_le_chemin_complet_d_une_propriete_imbriquee()
    {
        // Le membre feuille seul rend deux champs distincts indiscernables : x.Depense.Montant
        // et x.Budget.Plafond.Montant produiraient tous deux « 'Montant' ne doit pas être
        // vide. », sans que l'utilisateur sache lequel corriger.
        Resoudre((RequeteImbriquee r) => r.Depense.Montant).Should().Be("Depense.Montant");
    }

    [Fact]
    public void Applique_l_etiquette_Display_au_segment_qui_la_porte()
    {
        Resoudre((RequeteImbriquee r) => r.Budget.Plafond.Montant)
            .Should().Be("Budget.budget.Montant");
    }

    /// <summary>
    /// Type de ressources ne déclarant volontairement aucune clé : une étiquette qui pointe
    /// dessus est irrésolvable.
    /// </summary>
    public static class RessourcesFactices;

    private sealed class RequeteMalEtiquetee
    {
        [Display(Name = "CleAbsente", ResourceType = typeof(RessourcesFactices))]
        public string Montant { get; init; } = "";
    }

    [Fact]
    public void Se_replie_sur_l_identifiant_quand_l_etiquette_Display_est_irresolvable()
    {
        // DisplayAttribute.GetName() lève InvalidOperationException quand la clé de ressource
        // n'existe pas. Le résolveur étant appelé pendant ValidateAsync, laisser l'exception
        // remonter donnerait à l'appelant une InvalidOperationException au lieu d'une
        // ValidationException — donc, au bord HTTP, un 500 sur une requête mal remplie.
        Resoudre((RequeteMalEtiquetee r) => r.Montant).Should().Be("Montant");
    }

    /// <summary>
    /// Type de ressources dont la clé existe et se résout, mais dont le getter échoue à
    /// l'exécution — cas d'un type généré depuis un <c>.resx</c> dont le <c>.resources</c>
    /// compilé ou le satellite de culture manque au déploiement.
    /// </summary>
    public static class RessourcesQuiLevent
    {
        public static string Cle =>
            throw new MissingManifestResourceException("Satellite de ressources absent.");
    }

    private sealed class RequeteDontLaRessourceLeve
    {
        [Display(Name = "Cle", ResourceType = typeof(RessourcesQuiLevent))]
        public string Montant { get; init; } = "";
    }

    [Fact]
    public void Se_replie_sur_l_identifiant_quand_le_getter_de_ressource_leve()
    {
        // Symétrique du cas « clé absente », mais par un autre mécanisme : l'étiquette est
        // techniquement résolvable, et c'est l'invocation du getter qui échoue. GetName()
        // passant par la réflexion, l'échec ressort emballé dans TargetInvocationException —
        // que le garde d'origine, ciblé sur InvalidOperationException, ne rattrapait pas.
        Resoudre((RequeteDontLaRessourceLeve r) => r.Montant).Should().Be("Montant");
    }

    [Fact]
    public void Se_replie_sur_le_membre_feuille_sans_expression()
    {
        var feuille = typeof(Requete).GetProperty(nameof(Requete.SansEtiquette));

        ResolveurNomAffichable.Resoudre(feuille, expression: null)
            .Should().Be("SansEtiquette");
    }

    [Fact]
    public void Se_replie_sur_le_membre_feuille_quand_la_chaine_n_est_pas_enracinee_sur_le_parametre()
    {
        // Chaîne enracinée sur une variable capturée : composer le chemin ferait remonter le
        // nom du champ de fermeture jusqu'à l'écran.
        var depense = new Depense();
        Expression<Func<decimal>> acces = () => depense.Montant;

        ResolveurNomAffichable.Resoudre(((MemberExpression)acces.Body).Member, acces)
            .Should().Be("Montant");
    }

    /// <summary>
    /// Reproduit l'appel que FluentValidation fait au résolveur : le membre feuille de
    /// l'expression, et l'expression elle-même.
    /// </summary>
    private static string? Resoudre<T, TProp>(Expression<Func<T, TProp>> expression)
    {
        var feuille = (MemberInfo?)(expression.Body as MemberExpression)?.Member;

        return ResolveurNomAffichable.Resoudre(feuille, expression);
    }
}
