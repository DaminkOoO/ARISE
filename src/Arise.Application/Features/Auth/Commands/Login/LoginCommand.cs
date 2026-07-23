using MediatR;

namespace Arise.Application.Features.Auth.Commands.Login;

public sealed record LoginCommand(string Username, string Password) : IRequest<LoginResult>;

/// <summary>
/// Ce que la connexion renvoie : le jeton et sa date de péremption. Ni empreinte, ni mot de
/// passe — un résultat n'a aucune raison de les faire voyager jusqu'au bord HTTP, où ils
/// finiraient dans un journal.
/// </summary>
public sealed record LoginResult(string AccessToken, DateTimeOffset ExpiresAt);
