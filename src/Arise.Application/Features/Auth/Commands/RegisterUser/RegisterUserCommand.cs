using MediatR;

namespace Arise.Application.Features.Auth.Commands.RegisterUser;

public sealed record RegisterUserCommand(string Username, string Password)
    : IRequest<RegisterUserResult>;

/// <summary>
/// Ce que l'inscription renvoie : de quoi identifier le compte créé, et rien de plus. Ni
/// mot de passe, ni empreinte — un résultat n'a aucune raison de faire voyager l'un ou
/// l'autre jusqu'au bord HTTP, où il finirait dans un journal.
/// </summary>
public sealed record RegisterUserResult(Guid UserId, string Username);
