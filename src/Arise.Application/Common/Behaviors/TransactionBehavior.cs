using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Messaging;
using MediatR;

namespace Arise.Application.Common.Behaviors;

/// <summary>
/// Enveloppe toute commande dans une transaction : elle écrit tout, ou elle n'écrit rien.
///
/// <para>Avant lui, <c>CompleteGymQuestCommand</c> produisait trois écritures dans trois
/// transactions indépendantes — la quête, l'XP, la série. Une panne entre la première et la
/// deuxième laissait la quête accomplie sans son gain, et le Chasseur ne pouvait pas se
/// rattraper : au retap, la garde d'idempotence lui répondait « déjà complétée » pour un XP
/// qui n'était sur aucune ligne. Tolérable pour une quête ; plus du tout pour une écriture de
/// Budget, où c'est un solde faux que le Chasseur lirait.</para>
///
/// <para><b>Contraint aux commandes</b> — <c>where TRequest : ICommand&lt;TResponse&gt;</c> —
/// et non enregistré en générique ouvert sans condition : le conteneur .NET écarte de lui-même
/// les fermetures qui ne satisfont pas la contrainte, si bien qu'une requête ne le voit jamais
/// passer. Une lecture n'a rien à valider, et lui ouvrir une transaction ferait payer à chaque
/// affichage un aller-retour <c>BEGIN</c>/<c>COMMIT</c> pour rien.</para>
///
/// <para>Il est enregistré <b>après</b> <c>ValidationBehavior</c> : une commande refusée par
/// ses validators n'a jamais ouvert de transaction.</para>
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork uniteDeTravail)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Commande imbriquée : elle rejoint la transaction de sa parente et ne touche à rien.
        // CompleteGymQuestCommand envoie AwardXpCommand par MediatR — ouvrir une seconde
        // transaction ici, c'est la valider avant la fin de la première, donc rendre l'XP
        // définitif alors que la complétion de la quête peut encore échouer. Seule la commande
        // la plus externe ouvre et tranche.
        if (uniteDeTravail.TransactionEnCours)
        {
            return await next(cancellationToken);
        }

        await uniteDeTravail.CommencerAsync(cancellationToken);

        try
        {
            var reponse = await next(cancellationToken);

            // Après le handler, donc après la publication des événements de domaine, que le
            // dispatcher fait à l'intérieur de la transaction : la série que StreakUpdateHandler
            // écrit entre dans la même atomicité que l'XP et la complétion.
            await uniteDeTravail.ValiderAsync(cancellationToken);

            return reponse;
        }
        catch
        {
            // On annule, puis on laisse remonter : l'appelant doit voir la panne réelle, et le
            // bord HTTP la traduire. Rien n'est avalé ici.
            await uniteDeTravail.AnnulerAsync(cancellationToken);

            throw;
        }
    }
}
