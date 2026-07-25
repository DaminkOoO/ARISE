using Arise.Application.Common.Messaging;
using Arise.Domain.Hunters;

namespace Arise.Application.Features.Hunters.Commands.AwardXp;

/// <summary>
/// Attribue de l'XP au profil d'un Chasseur — quelle que soit la source (sport, budget,
/// habitude, calendrier) : c'est le point d'entrée unique du moteur de progression, jamais
/// dupliqué par domaine appelant.
/// </summary>
public sealed record AwardXpCommand(Guid HunterProfileId, int Montant) : ICommand<AwardXpResult>;

/// <summary>
/// L'état du profil après attribution — de quoi rafraîchir l'affichage sans requête
/// supplémentaire, rien de plus.
/// </summary>
public sealed record AwardXpResult(
    Guid HunterProfileId,
    int Level,
    HunterRank Rank,
    int CurrentXp,
    int XpToNextLevel);
