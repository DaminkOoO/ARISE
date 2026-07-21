using System.Linq.Expressions;
using System.Reflection;
// Alias : le namespace DataAnnotations expose lui aussi une ValidationException, qui
// entrerait en collision avec celle de FluentValidation.
using DisplayAttribute = System.ComponentModel.DataAnnotations.DisplayAttribute;

namespace Arise.Application.Common.Validation;

/// <summary>
/// Étiquette française d'une propriété dans les messages de validation.
///
/// <para>La seule source d'un libellé destiné à l'écran est une déclaration explicite :
/// <c>[Display(Name = "…")]</c> sur la propriété, ou <c>.WithName(…)</c> sur la règle, que
/// FluentValidation applique par-dessus ce résolveur.</para>
///
/// <para>Sans étiquette, on renvoie l'identifiant brut plutôt que son découpage PascalCase.
/// Ce découpage n'existe que pour transformer un identifiant anglais en prose anglaise :
/// appliqué ici, il fabrique un franglais présentable (« Nom Du Chasseur », « Email
/// Address ») qui traverse une revue sans se faire remarquer. L'identifiant brut, lui,
/// signale l'étiquette manquante.</para>
///
/// <para>Fonction pure, délibérément séparée du câblage : elle s'éprouve sans passer par
/// <c>ValidatorOptions.Global</c>, statique global au processus que xUnit ferait fuir d'une
/// classe de tests parallélisée à l'autre.</para>
/// </summary>
internal static class ResolveurNomAffichable
{
    internal static string? Resoudre(MemberInfo? membre, LambdaExpression? expression) =>
        membre is null
            ? null // Expression sans membre : on laisse FluentValidation décider.
            : Etiquette(membre);

    private static string Etiquette(MemberInfo membre) =>
        membre.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? membre.Name;
}
