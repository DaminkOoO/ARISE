using System.Linq.Expressions;
using System.Reflection;
using System.Resources;
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
        Chemin(expression)
        ?? (membre is null
            ? null // Ni chemin ni membre : on laisse FluentValidation décider.
            : Etiquette(membre));

    /// <summary>
    /// Chemin complet, segments joints par un point, d'une expression d'accès membre.
    ///
    /// <para>Le membre feuille seul rend indiscernables deux champs homonymes :
    /// <c>x.Depense.Montant</c> et <c>x.Budget.Montant</c> produiraient le même « 'Montant'
    /// ne doit pas être vide. », sans dire lequel corriger.</para>
    ///
    /// <para>Renvoie <c>null</c> — et non une valeur approchée — dès que l'expression n'est
    /// pas une chaîne d'accès enracinée sur le paramètre du lambda (appel de méthode,
    /// indexeur, capture de variable). L'appelant se replie alors sur le membre feuille.</para>
    /// </summary>
    private static string? Chemin(LambdaExpression? expression)
    {
        if (expression is null)
        {
            return null;
        }

        // Empilés feuille d'abord, la pile les restitue donc dans l'ordre de lecture.
        var segments = new Stack<MemberInfo>();
        var courante = Deballe(expression.Body);

        while (courante is MemberExpression acces)
        {
            segments.Push(acces.Member);
            courante = Deballe(acces.Expression);
        }

        return segments.Count != 0 && courante is ParameterExpression
            ? string.Join('.', segments.Select(Etiquette))
            : null;
    }

    /// <summary>
    /// Retire la conversion que le compilateur insère quand une propriété de type valeur est
    /// capturée dans une expression à résultat <c>object</c>.
    /// </summary>
    private static Expression? Deballe(Expression? expression) =>
        expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } conversion
            ? conversion.Operand
            : expression;

    /// <summary>
    /// Libellé d'un segment : son <c>[Display(Name = "…")]</c> s'il en porte un, sinon son
    /// identifiant brut.
    ///
    /// <para>Ce résolveur s'exécutant au cœur de <c>ValidateAsync</c>, laisser une exception
    /// remonter substituerait une panne technique à la <c>ValidationException</c> attendue —
    /// soit, au bord HTTP, un 500 sur une simple requête mal remplie. Une étiquette cassée
    /// dégrade donc le libellé, pas la requête.</para>
    ///
    /// <para>Deux modes de panne distincts, et le second n'est pas une variante du premier :
    /// <list type="bullet">
    /// <item>la clé est introuvable dans le type de ressource — <c>GetName()</c> lève
    /// <see cref="InvalidOperationException"/> avant même de lire quoi que ce soit ;</item>
    /// <item>la clé se résout, mais son getter échoue à l'exécution — typiquement un type
    /// généré depuis un <c>.resx</c> dont le <c>.resources</c> compilé ou le satellite de
    /// culture manque au déploiement (image Docker construite sans les satellites,
    /// <c>&lt;EmbeddedResource&gt;</c> retiré). L'exception d'origine est alors une
    /// <see cref="MissingManifestResourceException"/>.</item>
    /// </list></para>
    ///
    /// <para>Ce second cas se rattrape sous deux formes selon le chemin d'invocation : la
    /// réflexion emballe l'échec du getter dans une
    /// <see cref="TargetInvocationException"/> lorsqu'elle enveloppe les exceptions, et le
    /// laisse ressortir tel quel sinon (ce que fait le chemin actuel du runtime). Les deux
    /// sont couvertes ; on ne parie pas sur la forme observée aujourd'hui.</para>
    ///
    /// <para>Le rattrapage est large mais borné à ces types : la valeur de repli
    /// (<c>membre.Name</c>) est toujours correcte, et aucune de ces pannes n'est actionnable
    /// pour l'appelant, qui voulait un libellé et en obtient un.</para>
    /// </summary>
    private static string Etiquette(MemberInfo membre)
    {
        try
        {
            return membre.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? membre.Name;
        }
        catch (Exception exception) when (exception
            is InvalidOperationException
            or MissingManifestResourceException
            or TargetInvocationException)
        {
            return membre.Name;
        }
    }
}
