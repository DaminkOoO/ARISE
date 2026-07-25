using Arise.Domain.Common;
using MediatR;

namespace Arise.Application.Common.Events;

/// <summary>
/// Le pont entre un <see cref="IDomainEvent"/> (Domain, qui ne référence pas MediatR) et une
/// notification MediatR publiable (Application, qui le référence). Un événement de domaine
/// accumulé sur une entité est enveloppé dans cette notification au moment de la publication
/// — l'entité elle-même ne sait jamais comment ses faits voyagent.
///
/// <para><b>Générique</b>, et pas une enveloppe unique : un abonné s'inscrit ainsi au seul
/// événement qui l'intéresse. Avec une enveloppe non paramétrée, tout
/// <see cref="INotificationHandler{TNotification}"/> recevrait chaque fait du dépôt et devrait
/// trancher par un test de type dans son corps — le handler de série se réveillerait à chaque
/// montée de rang.</para>
/// </summary>
public sealed record DomainEventNotification<TEvent>(TEvent DomainEvent) : INotification
    where TEvent : IDomainEvent;

/// <summary>
/// Fabrique les enveloppes. Les entités accumulent leurs faits dans une même liste d'
/// <see cref="IDomainEvent"/> : refermer le générique sur le type réel demande donc de le
/// résoudre à l'exécution, et c'est le seul endroit du dépôt qui le fasse.
/// </summary>
public static class DomainEventNotification
{
    /// <summary>
    /// Enveloppe <paramref name="domainEvent"/> dans un
    /// <see cref="DomainEventNotification{TEvent}"/> refermé sur son <b>type réel</b>, et non sur
    /// <see cref="IDomainEvent"/> : c'est ce type qui décide des abonnés réveillés.
    ///
    /// <para>Réflexion plutôt que <c>dynamic</c> : le résultat est un
    /// <see cref="INotification"/> ordinaire, dont la résolution est explicite et lisible ici,
    /// là où un <c>dynamic</c> aurait déplacé la même résolution dans le binder du runtime, hors
    /// de vue du site d'appel. MediatR route ensuite sur le type réel de l'instance publiée, ce
    /// que le test <c>Atteint_l_abonne_du_type_concret_a_travers_MediatR</c> tient sous
    /// surveillance — publier sans abonné est autrement parfaitement silencieux.</para>
    /// </summary>
    public static INotification Envelopper(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var typeEnveloppe = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());

        return (INotification)Activator.CreateInstance(typeEnveloppe, domainEvent)!;
    }
}
