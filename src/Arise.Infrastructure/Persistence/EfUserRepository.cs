using Arise.Application.Common.Abstractions;
using Arise.Domain.Users;
using Microsoft.EntityFrameworkCore;

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
        await context.SaveChangesAsync(cancellationToken);
    }
}
