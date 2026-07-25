using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Events;
using Arise.Application.Common.Exceptions;
using MediatR;

namespace Arise.Application.Features.Hunters.Commands.AwardXp;

/// <summary>
/// Attribue de l'XP à un profil de Chasseur. Toute la logique de progression (seuils de
/// niveau, franchissement de rang) vit dans <see cref="Arise.Domain.Hunters.HunterProfile"/> ;
/// ce handler ne fait que l'orchestrer : charger, déléguer, sauvegarder, publier.
/// </summary>
public sealed class AwardXpCommandHandler(
    IHunterProfileRepository hunterProfiles,
    IPublisher publisher)
    : IRequestHandler<AwardXpCommand, AwardXpResult>
{
    public async Task<AwardXpResult> Handle(
        AwardXpCommand request,
        CancellationToken cancellationToken)
    {
        var profile = await hunterProfiles.GetByIdAsync(request.HunterProfileId, cancellationToken)
            ?? throw new HunterProfileNotFoundException();

        profile.AwardXp(request.Montant);

        await hunterProfiles.SaveAsync(profile, cancellationToken);

        // Un événement par rang franchi, pas un seul coalescé : un gros gain d'XP (Boss Raid)
        // peut traverser plusieurs frontières de rang d'un coup, et chacune doit déclencher
        // sa propre réaction (notification, déblocage) en aval.
        foreach (var domainEvent in profile.DomainEvents)
        {
            await publisher.Publish(
                DomainEventNotification.Envelopper(domainEvent), cancellationToken);
        }

        profile.ClearDomainEvents();

        return new AwardXpResult(
            profile.Id, profile.Level, profile.Rank, profile.CurrentXp, profile.XpToNextLevel);
    }
}
