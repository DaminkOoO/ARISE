using Arise.Domain.Hunters;

namespace Arise.Application.Common.Abstractions;

/// <summary>
/// Accès au profil de progression d'un Chasseur. Implémenté dans la couche Infrastructure :
/// la couche Application ignore qu'il y a un PostgreSQL derrière.
/// </summary>
public interface IHunterProfileRepository
{
    /// <summary>Le profil portant cet identifiant, ou <c>null</c> s'il n'y en a pas.</summary>
    Task<HunterProfile?> GetByIdAsync(Guid hunterProfileId, CancellationToken cancellationToken);

    Task SaveAsync(HunterProfile hunterProfile, CancellationToken cancellationToken);
}
