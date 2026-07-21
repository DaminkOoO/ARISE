using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Arise.Application.Common.Behaviors;

/// <summary>
/// Exécute tous les <see cref="IValidator{T}"/> enregistrés pour une requête avant de la
/// laisser atteindre son handler. La validation vit donc dans le pipeline, jamais dans le
/// handler : celui-ci ne voit que des requêtes déjà valides.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Les erreurs de tous les validators sont agrégées : l'utilisateur voit l'ensemble
        // des corrections à faire d'un coup, pas une seule à la fois.
        var erreurs = new List<ValidationFailure>();

        foreach (var validator in validators)
        {
            // Un contexte neuf par validator : un contexte partagé accumule les échecs et
            // les fait remonter en double. Exécution séquentielle et non parallèle — un
            // validator qui interroge la base réutiliserait le DbContext, non thread-safe.
            var resultat = await validator.ValidateAsync(
                new ValidationContext<TRequest>(request), cancellationToken);

            erreurs.AddRange(resultat.Errors);
        }

        if (erreurs.Count != 0)
        {
            throw new ValidationException(erreurs);
        }

        return await next(cancellationToken);
    }
}
