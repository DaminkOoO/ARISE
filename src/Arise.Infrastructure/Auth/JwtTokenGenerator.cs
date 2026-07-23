using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Arise.Application.Common.Abstractions;
using Arise.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Arise.Infrastructure.Auth;

/// <summary>
/// Émet un JWT signé en HS256 à partir des paramètres de configuration (<see cref="JwtOptions"/>).
/// Le sujet porte l'Id du Chasseur ; l'expiration est datée depuis une horloge injectée, pour
/// rester testable à instant figé.
/// </summary>
internal sealed class JwtTokenGenerator(IOptions<JwtOptions> options, TimeProvider clock)
    : IJwtTokenGenerator
{
    public JwtToken Generate(User user)
    {
        var config = options.Value;

        var maintenant = clock.GetUtcNow();
        var expiration = maintenant.AddMinutes(config.DureeMinutes);

        var identifiants = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.Key)),
            SecurityAlgorithms.HmacSha256);

        var jeton = new JwtSecurityToken(
            issuer: config.Issuer,
            audience: config.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ],
            notBefore: maintenant.UtcDateTime,
            expires: expiration.UtcDateTime,
            signingCredentials: identifiants);

        var valeur = new JwtSecurityTokenHandler().WriteToken(jeton);

        // token.ValidTo est l'exp réellement inscrit (secondes Unix, tronqué) : le renvoyer tel
        // quel garantit que ExpiresAt ne promet pas une seconde de plus que le jeton n'en porte.
        return new JwtToken(valeur, new DateTimeOffset(jeton.ValidTo, TimeSpan.Zero));
    }
}
