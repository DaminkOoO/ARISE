using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Domain.Users;
using MediatR;

namespace Arise.Application.Features.Auth.Commands.RegisterUser;

/// <summary>
/// Inscrit un Chasseur : le mot de passe reçu est haché ici et n'est jamais passé plus loin.
///
/// <para>Le contrôle d'unicité qui suit est un confort d'affichage, pas une garantie : entre
/// la lecture et l'écriture, une seconde inscription du même nom peut se glisser. Ce qui
/// tranche réellement est l'index unique sur la colonne, posé avec le
/// <c>AriseDbContext</c> — ici, on se contente d'expliquer le conflit en français plutôt que
/// de laisser remonter une violation de contrainte.</para>
/// </summary>
public sealed class RegisterUserCommandHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider)
    : IRequestHandler<RegisterUserCommand, RegisterUserResult>
{
    public async Task<RegisterUserResult> Handle(
        RegisterUserCommand request,
        CancellationToken cancellationToken)
    {
        // Le nom rogné est celui que portera le compte : chercher le brut laisserait passer
        // « sung » puis « sung  » comme deux inscriptions distinctes.
        var username = request.Username.Trim();

        if (await users.ExistsWithUsernameAsync(username, cancellationToken))
        {
            throw new UsernameAlreadyTakenException();
        }

        var user = User.Register(
            username,
            passwordHasher.Hash(request.Password),
            timeProvider.GetUtcNow());

        await users.AddAsync(user, cancellationToken);

        return new RegisterUserResult(user.Id, user.Username);
    }
}
