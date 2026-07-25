using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Events;
using Arise.Application.Common.Exceptions;
using MediatR;

namespace Arise.Application.Features.Hunters.Commands.AwardXp;

/// <summary>
/// Attribue de l'XP à un profil de Chasseur. Toute la logique de progression (seuils de
/// niveau, franchissement de rang) vit dans <see cref="Arise.Domain.Hunters.HunterProfile"/> ;
/// ce handler ne fait que l'orchestrer : charger, déléguer, sauvegarder, publier.
///
/// <para>Et rejouer, quand une attribution simultanée a gagné la course. Deux gains d'XP au
/// même instant — la quête de la veille et celle du jour, demain le Sport et les Habitudes —
/// lisent le même total et écrivent chacun le leur : le jeton de concurrence refuse le second,
/// le repository rafraîchit le profil avec l'état gagnant, et l'attribution se rejoue
/// par-dessus. Un gain d'XP ne se perd pas parce que deux chemins sont arrivés ensemble.</para>
/// </summary>
public sealed class AwardXpCommandHandler(
    IHunterProfileRepository hunterProfiles,
    IPublisher publisher)
    : IRequestHandler<AwardXpCommand, AwardXpResult>
{
    /// <summary>
    /// Le rejeu est borné : une base durablement contendue ne doit pas faire tourner un handler
    /// indéfiniment. Trois tentatives suffisent largement à une course entre deux écritures ;
    /// au-delà, l'échec remonte plutôt que de se cacher.
    /// </summary>
    private const int TentativesMaximales = 3;

    public async Task<AwardXpResult> Handle(
        AwardXpCommand request,
        CancellationToken cancellationToken)
    {
        for (var tentative = 1; ; tentative++)
        {
            // Relu à chaque tentative : après une écriture perdue, le repository a rafraîchi le
            // profil avec l'état gagnant, et c'est par-dessus celui-là que le montant s'applique.
            var profile = await hunterProfiles.GetByIdAsync(
                    request.HunterProfileId, cancellationToken)
                ?? throw new HunterProfileNotFoundException();

            profile.AwardXp(request.Montant);

            try
            {
                await hunterProfiles.SaveAsync(profile, cancellationToken);
            }
            catch (ConcurrentHunterProfileUpdateException) when (tentative < TentativesMaximales)
            {
                continue;
            }

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
}
