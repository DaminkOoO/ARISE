using Arise.Domain.Common;

namespace Arise.Domain.Hunters;

/// <summary>
/// Fait qu'un Chasseur a franchi un seuil de rang suite à un gain d'XP. Un événement est levé
/// par rang franchi — un appel à <see cref="HunterProfile.AwardXp"/> qui traverse plusieurs
/// frontières de rang d'un coup en lève donc plusieurs, un par palier intermédiaire.
/// </summary>
public sealed record HunterRankedUpEvent(
    Guid HunterProfileId,
    HunterRank AncienRang,
    HunterRank NouveauRang) : IDomainEvent;
