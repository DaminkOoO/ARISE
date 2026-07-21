using System.Globalization;
using System.Reflection;
using Arise.Application.Common.Behaviors;
using Arise.Application.Common.Validation;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

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
            (_, membre, expression) => ResolveurNomAffichable.Resoudre(membre, expression);

        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(AssemblyApplication));

        services.AddValidatorsFromAssembly(AssemblyApplication, includeInternalTypes: true);

        // Enregistré en générique ouvert : toute commande ou requête ajoutée plus tard
        // traverse la validation sans câblage supplémentaire.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
