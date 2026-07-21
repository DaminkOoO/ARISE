using System.Globalization;
using System.Reflection;
using Arise.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
// Alias : le namespace DataAnnotations expose lui aussi une ValidationException, qui
// entrerait en collision avec celle de FluentValidation.
using DisplayAttribute = System.ComponentModel.DataAnnotations.DisplayAttribute;

namespace Arise.Application;

/// <summary>
/// Point d'entrée unique du câblage de la couche Application. L'API ne connaît donc ni
/// MediatR ni FluentValidation : elle appelle <see cref="AddApplication"/> et rien d'autre.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Assembly balayée pour découvrir les handlers et les validators.
    /// </summary>
    public static Assembly AssemblyApplication { get; } =
        typeof(DependencyInjection).Assembly;

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Règle non négociable n°7 : les messages de validation par défaut de
        // FluentValidation remontent jusqu'à l'écran, ils doivent donc être en français.
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("fr");

        // Traduire le gabarit ne suffit pas : le {PropertyName} interpolé dedans en fait
        // partie, et le résoudre par découpage PascalCase de l'identifiant C# produit du
        // franglais ('Password' ne doit pas être vide, 'Email Address' n'est pas valide).
        ValidatorOptions.Global.DisplayNameResolver =
            (_, membre, _) => NomAffichable(membre);

        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(AssemblyApplication));

        services.AddValidatorsFromAssembly(AssemblyApplication, includeInternalTypes: true);

        // Enregistré en générique ouvert : toute commande ou requête ajoutée plus tard
        // traverse la validation sans câblage supplémentaire.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }

    /// <summary>
    /// Étiquette française d'une propriété dans les messages de validation.
    ///
    /// <para>La seule source d'un libellé destiné à l'écran est une déclaration explicite :
    /// <c>[Display(Name = "…")]</c> sur la propriété, ou <c>.WithName(…)</c> sur la règle,
    /// que FluentValidation applique par-dessus ce résolveur.</para>
    ///
    /// <para>Sans étiquette, on renvoie l'identifiant brut plutôt que son découpage
    /// PascalCase. Ce découpage n'existe que pour transformer un identifiant anglais en
    /// prose anglaise : appliqué ici, il fabrique un franglais présentable
    /// (« Nom Du Chasseur », « Email Address ») qui traverse une revue sans se faire
    /// remarquer. L'identifiant brut, lui, signale l'étiquette manquante.</para>
    /// </summary>
    private static string? NomAffichable(MemberInfo? membre) =>
        membre is null
            ? null // Expression sans membre : on laisse FluentValidation décider.
            : membre.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? membre.Name;
}
