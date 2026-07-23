using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Arise.Domain.Users;
using Arise.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Arise.Infrastructure.Tests.Auth;

/// <summary>
/// Éprouve l'émission des jetons : le sujet porte bien l'Id du Chasseur, l'expiration reflète
/// la durée configurée, et la signature tient contre la clé configurée. On décode et valide le
/// jeton avec la même bibliothèque que le middleware JwtBearer — pas de vérification maison.
/// </summary>
public class JwtTokenGeneratorTests
{
    private const string Cle = "clé-de-test-arise-au-moins-256-bits-pour-hmac-sha256";
    private const string Emetteur = "arise-tests";
    private const string Audience = "arise-clients-tests";

    private static readonly DateTimeOffset Maintenant =
        new(2026, 7, 23, 20, 0, 0, TimeSpan.Zero);

    private static JwtTokenGenerator Generateur(int dureeMinutes = 60) =>
        new(
            Options.Create(new JwtOptions
            {
                Key = Cle,
                Issuer = Emetteur,
                Audience = Audience,
                DureeMinutes = dureeMinutes,
            }),
            new HorlogeFigee(Maintenant));

    private static User UnChasseur() =>
        User.Register("Sung", "empreinte-scellée", Maintenant);

    [Fact]
    public void Porte_l_Id_du_Chasseur_comme_sujet()
    {
        var chasseur = UnChasseur();

        var jeton = Generateur().Generate(chasseur);

        var lu = new JwtSecurityTokenHandler().ReadJwtToken(jeton.Value);
        lu.Subject.Should().Be(chasseur.Id.ToString());
    }

    [Fact]
    public void Date_l_expiration_a_l_horloge_plus_la_duree_configuree()
    {
        var jeton = Generateur(dureeMinutes: 90).Generate(UnChasseur());

        jeton.ExpiresAt.Should().Be(Maintenant.AddMinutes(90));
    }

    [Fact]
    public void Signe_le_jeton_avec_la_cle_configuree()
    {
        var jeton = Generateur().Generate(UnChasseur());

        var parametres = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Cle)),
            ValidIssuer = Emetteur,
            ValidAudience = Audience,
            ValidateLifetime = false,
        };

        var validation = () => new JwtSecurityTokenHandler()
            .ValidateToken(jeton.Value, parametres, out _);

        validation.Should().NotThrow();
    }

    private sealed class HorlogeFigee(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }
}
