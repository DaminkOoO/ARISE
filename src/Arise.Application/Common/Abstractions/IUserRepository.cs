using Arise.Domain.Users;

namespace Arise.Application.Common.Abstractions;

/// <summary>
/// Accès aux comptes de Chasseurs. Implémenté dans la couche Infrastructure : la couche
/// Application ignore qu'il y a un PostgreSQL derrière.
///
/// <para>Le nom d'utilisateur se compare <b>sans distinction de casse</b> — « Sung » et
/// « sung » désignent le même Chasseur, sur les deux méthodes. C'est un point du contrat,
/// pas un détail d'implémentation : sans lui, deux comptes homographes coexistent et le
/// second ne peut plus se connecter.</para>
/// </summary>
public interface IUserRepository
{
    /// <summary>Un compte porte-t-il déjà ce nom ?</summary>
    Task<bool> ExistsWithUsernameAsync(string username, CancellationToken cancellationToken);

    /// <summary>Le Chasseur portant ce nom, ou <c>null</c> s'il n'y en a pas.</summary>
    Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken);

    Task AddAsync(User user, CancellationToken cancellationToken);
}
