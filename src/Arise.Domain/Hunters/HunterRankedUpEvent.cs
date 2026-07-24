using Arise.Domain.Common;

namespace Arise.Domain.Hunters;

/// <summary>
/// Fait qu'un Chasseur a franchi un seuil de rang suite à un gain d'XP. Levé au plus une fois
/// par appel à <see cref="HunterProfile.AwardXp"/>, même si ce gain fait monter plusieurs
/// niveaux d'un coup — seul le rang final compte.
/// </summary>
public sealed record HunterRankedUpEvent(
    Guid HunterProfileId,
    HunterRank AncienRang,
    HunterRank NouveauRang) : IDomainEvent;
