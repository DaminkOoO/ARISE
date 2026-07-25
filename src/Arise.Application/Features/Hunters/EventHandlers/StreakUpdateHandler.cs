using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Events;
using Arise.Application.Common.Exceptions;
using Arise.Domain.Quests;
using MediatR;

namespace Arise.Application.Features.Hunters.EventHandlers;

/// <summary>
/// Tient la série d'engagement du Chasseur à jour à chaque quête complétée.
///
/// <para>Handler <b>central</b>, et c'est tout son intérêt (doc mécaniques, section 2) : peu
/// importe quel handler de commande — Sport aujourd'hui, Budget, Habitudes et Calendrier
/// demain — a déclenché la complétion, la logique de série reste à un seul endroit. Un
/// <c>RegisterDailyCompletion</c> recopié dans chaque handler de complétion produirait quatre
/// exemplaires de la même règle, et le jour où l'un change, trois l'oublient.</para>
///
/// <para>La série compte des <b>jours</b>, pas des quêtes : deux complétions dans la même
/// journée du Chasseur ne la font monter qu'une fois, et c'est l'entité qui le garantit.</para>
/// </summary>
public sealed class StreakUpdateHandler(IHunterProfileRepository hunterProfiles)
    : INotificationHandler<DomainEventNotification<QuestCompletedEvent>>
{
    public async Task Handle(
        DomainEventNotification<QuestCompletedEvent> notification,
        CancellationToken cancellationToken)
    {
        var completion = notification.DomainEvent;

        var profil = await hunterProfiles.GetByIdAsync(completion.HunterProfileId, cancellationToken)
            ?? throw new HunterProfileNotFoundException();

        // Les deux types de quête que le produit connaît comptent tous deux pour la série
        // (doc mécaniques, section 2). Une quête de pénalité n'est pas une punition : elle est
        // volontairement facile pour redonner un point d'appui, et la décompter irait contre son
        // propos. Le jour où un type qui ne compte pas apparaîtra — un Boss Raid hebdomadaire —,
        // c'est ici, et seulement ici, qu'il faudra le filtrer.
        //
        // La date vient de l'événement et non d'une horloge : seul le producteur de la
        // complétion connaît le fuseau du Chasseur, et c'est à ce jour-là que la série se compte.
        profil.RegisterDailyCompletion(completion.JourDuChasseur);

        await hunterProfiles.SaveAsync(profil, cancellationToken);
    }
}
