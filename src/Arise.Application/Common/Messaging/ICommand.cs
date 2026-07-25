using MediatR;

namespace Arise.Application.Common.Messaging;

/// <summary>
/// Un message qui <b>écrit</b>. Marqueur, sans membre : il n'ajoute rien à
/// <see cref="IRequest{TResponse}"/> côté appelant — <c>ISender.Send</c> continue de le router
/// comme n'importe quelle requête MediatR.
///
/// <para>Ce qu'il ajoute est un fait de type, exploitable par le pipeline : c'est lui qui
/// laisse <c>TransactionBehavior</c> se contraindre aux seules écritures. Sans ce tri, un
/// behavior en générique ouvert envelopperait aussi les lectures dans une transaction — et le
/// jour où une lecture serait rangée du mauvais côté, rien ne le dirait.</para>
/// </summary>
public interface ICommand<out TResponse> : IRequest<TResponse>;
