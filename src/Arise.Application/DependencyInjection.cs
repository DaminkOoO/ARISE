using System.Globalization;
using System.Reflection;
using Arise.Application.Common.Behaviors;
using Arise.Application.Common.Validation;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        // Les handlers datent ce qu'ils écrivent depuis cette horloge plutôt que depuis
        // DateTimeOffset.UtcNow, pour rester testables à instant figé. TryAdd, et non Add :
        // un hôte qui a déjà posé la sienne — un test de bout en bout sur une série de jours
        // ne peut pas attendre minuit — garde la sienne.
        services.TryAddSingleton(TimeProvider.System);

        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(AssemblyApplication));

        // includeInternalTypes est nécessaire : le validator de PipelineValidationProbe
        // (Common/Diagnostics) est internal, et c'est lui qui rend cette découverte
        // observable. Le retirer rendrait le balayage muet sans faire rougir grand-chose.
        services.AddValidatorsFromAssembly(AssemblyApplication, includeInternalTypes: true);

        // Enregistré en générique ouvert : toute commande ou requête ajoutée plus tard
        // traverse la validation sans câblage supplémentaire. TryAddEnumerable, et non
        // AddTransient : deux appels à AddApplication() empileraient sinon deux behaviors,
        // et chaque requête valide traverserait la validation en double.
        services.TryAddEnumerable(
            ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>)));

        return services;
    }
}
