using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Domain.Hunters;
using Microsoft.EntityFrameworkCore;

namespace Arise.Infrastructure.Persistence;

/// <summary>
/// Accès EF Core aux profils de progression. Contrairement à
/// <see cref="EfUserRepository"/>, <see cref="GetByIdAsync"/> ne passe pas par
/// <c>AsNoTracking</c> : <see cref="AwardXpCommandHandler"/> charge le profil, le mute en
/// mémoire (<c>AwardXp</c>), puis appelle <see cref="SaveAsync"/> sur cette même instance dans
/// le même scope — c'est le suivi de modifications d'EF Core qui doit détecter ces mutations,
/// pas une comparaison manuelle de champs.
/// </summary>
internal sealed class EfHunterProfileRepository(AriseDbContext context) : IHunterProfileRepository
{
    public Task<HunterProfile?> GetByIdAsync(Guid hunterProfileId, CancellationToken cancellationToken) =>
        context.HunterProfiles
            .SingleOrDefaultAsync(profile => profile.Id == hunterProfileId, cancellationToken);

    // Sert deux appelants distincts : OnboardHunterCommandHandler sauvegarde un profil flambant
    // neuf (jamais chargé, donc détaché du contexte) ; AwardXpCommandHandler sauvegarde un
    // profil déjà chargé par GetByIdAsync plus haut dans le même scope (donc déjà suivi). On ne
    // décide « nouveau vs existant » qu'à partir de l'état de suivi, jamais d'une requête
    // d'existence supplémentaire.
    public async Task SaveAsync(HunterProfile hunterProfile, CancellationToken cancellationToken)
    {
        if (context.Entry(hunterProfile).State == EntityState.Detached)
        {
            await context.HunterProfiles.AddAsync(hunterProfile, cancellationToken);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        // Le jeton de concurrence a refusé l'écriture : un autre gain d'XP a touché la ligne
        // entre notre lecture et notre sauvegarde. On rafraîchit le profil avec l'état gagnant
        // — et on jette les faits accumulés sur la tentative perdue, qui n'a pas eu lieu — pour
        // que le chemin d'écriture puisse rejouer son attribution par-dessus, plutôt que de
        // l'écraser. Traduit en vocabulaire métier : la couche Application n'a pas à connaître
        // DbUpdateConcurrencyException.
        catch (DbUpdateConcurrencyException)
        {
            await context.Entry(hunterProfile).ReloadAsync(cancellationToken);
            hunterProfile.ClearDomainEvents();

            throw new ConcurrentHunterProfileUpdateException();
        }
    }
}
