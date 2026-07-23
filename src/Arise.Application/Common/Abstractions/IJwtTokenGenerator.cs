using Arise.Domain.Users;

namespace Arise.Application.Common.Abstractions;

/// <summary>
/// Émission des jetons d'accès. La clé de signature et la durée de validité sont de la
/// configuration : elles vivent dans la couche Infrastructure, pas ici.
/// </summary>
public interface IJwtTokenGenerator
{
    JwtToken Generate(User user);
}

/// <summary>
/// Un jeton et l'instant où il cesse d'être accepté. L'expiration voyage à côté du jeton
/// pour que le client sache quand se reconnecter sans avoir à décoder la charge utile.
/// </summary>
public sealed record JwtToken(string Value, DateTimeOffset ExpiresAt);
