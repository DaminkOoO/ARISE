using MediatR;

namespace Arise.Application.Common.Messaging;

/// <summary>
/// Un message qui <b>lit</b> et ne mute rien. Pendant de <see cref="ICommand{TResponse}"/> :
/// les deux ensemble couvrent tout message MediatR de la couche, et un test de convention
/// (<c>MarqueursDeMessagesTests</c>) refuse qu'un message n'en porte aucun.
///
/// <para>Une requête ne traverse pas <c>TransactionBehavior</c> : une lecture n'a rien à
/// valider, et lui ouvrir une transaction ferait payer à chaque affichage un aller-retour de
/// <c>BEGIN</c>/<c>COMMIT</c> pour rien.</para>
/// </summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;
