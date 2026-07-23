using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using MediatR;

namespace Arise.Application.Features.Auth.Commands.Login;

/// <summary>
/// Authentifie un Chasseur et lui remet un jeton.
///
/// <para>Les deux causes d'échec — nom inconnu, mot de passe faux — lèvent la même exception
/// avec le même message : les distinguer transformerait la connexion en oracle de noms
/// existants.</para>
///
/// <para>Cette égalité porte sur ce qui est <b>dit</b>, pas sur le temps de réponse : le
/// chemin « nom inconnu » saute la vérification d'empreinte et répond donc plus vite. Aucune
/// empreinte factice n'est vérifiée pour égaliser ce temps, et c'est délibéré — l'inscription
/// annonce déjà franchement qu'un nom est pris (<see cref="UsernameAlreadyTakenException"/>),
/// puisqu'il faut bien que le Chasseur puisse en choisir un autre. Payer un hachage sur
/// chaque tentative fermerait un canal que la porte d'à côté laisse grande ouverte.</para>
/// </summary>
public sealed class LoginCommandHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator tokenGenerator)
    : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        // Le compte porte un nom rogné (User.Register) : chercher le brut refuserait la
        // connexion à qui a effleuré la barre d'espace après son nom.
        var user = await users.FindByUsernameAsync(
            request.Username.Trim(), cancellationToken);

        // Le mot de passe, lui, part tel qu'il a été saisi : le rogner amputerait une phrase
        // de passe qui commence ou finit par une espace délibérée.
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        var token = tokenGenerator.Generate(user);

        return new LoginResult(token.Value, token.ExpiresAt);
    }
}
