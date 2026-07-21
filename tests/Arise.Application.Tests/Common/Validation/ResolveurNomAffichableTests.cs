using System.Linq.Expressions;
using System.Reflection;
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
