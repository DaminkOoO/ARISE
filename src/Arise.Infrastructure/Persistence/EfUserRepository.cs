using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Arise.Infrastructure.Persistence;

/// <summary>
/// Accès EF Core aux comptes de Chasseurs. Rend des types du domaine, jamais un
/// <c>IQueryable</c> qui ferait fuir le provider vers la couche Application.
///
/// <para>La comparaison des noms est une <b>égalité simple</b> : la collation insensible à la
/// casse portée par la colonne (<see cref="AriseDbContext.CollationInsensibleALaCasse"/>) rend
/// « Sung » et « sung » égaux tout en gardant l'index unique. Un <c>ToLower()</c> ou un
/// <c>ILike</c> contournerait cet index et diverger du comportement de la contrainte.</para>
/// </summary>
internal sealed class EfUserRepository(AriseDbContext context) : IUserRepository
{
    // Lecture pure : pas de suivi de modifications à traîner.
    public Task<bool> ExistsWithUsernameAsync(string username, CancellationToken cancellationToken) =>
        context.Users
            .AsNoTracking()
            .AnyAsync(user => user.Username == username, cancellationToken);

    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken) =>
        context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Username == username, cancellationToken);

    // Le handler d'inscription ne fait pas de SaveChanges : chaque inscription est une
    // intention complète, l'écriture est validée ici même.
    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await context.Users.AddAsync(user, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        // La vérification préalable du handler ne couvre pas une course entre deux inscriptions
        // homographes : c'est l'index unique qui tranche vraiment, et il le fait ici, par une
        // violation 23505. On la traduit dans le vocabulaire métier — le bord HTTP sait afficher
        // ce conflit en 409 français, là où une DbUpdateException nue retomberait sur un 500 nu.
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new UsernameAlreadyTakenException();
        }
    }
}
