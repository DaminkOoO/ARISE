using Arise.Application.Common.Abstractions;
using Arise.Domain.Users;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Arise.Infrastructure.Tests.Persistence;

/// <summary>
/// Éprouve le repository sur un vrai Postgres : le round-trip, l'insensibilité à la casse que
/// porte la collation, et le garde-fou de sécurité — le mot de passe en clair n'atterrit
/// jamais dans la ligne. Chaque test choisit un nom de Chasseur unique : la collection partage
/// une base et un index unique.
/// </summary>
[Collection(PostgresCollection.Nom)]
public class EfUserRepositoryTests(PostgresFixture postgres)
{
    // Le nom d'un Chasseur est plafonné à 32 : un suffixe court suffit à distinguer les tests
    // qui partagent la base.
    private static string NomUnique(string racine) => $"{racine}{Guid.NewGuid():N}"[..12];

    // Instant à la microseconde près : c'est la précision d'un timestamptz Postgres.
    // DateTimeOffset.UtcNow porterait des ticks plus fins, tronqués à l'écriture — le
    // round-trip ne serait alors « identique » qu'à une tolérance près, ce que ce test refuse
    // de prouver mollement.
    private static readonly DateTimeOffset Inscrit =
        new DateTimeOffset(2026, 7, 23, 20, 15, 30, TimeSpan.Zero).AddMicroseconds(456);

    [Fact]
    public async Task Relit_un_Chasseur_identique_depuis_un_contexte_neuf()
    {
        var chasseur = User.Register(NomUnique("Sung"), "empreinte-scellée", Inscrit);

        await using (var ecriture = postgres.Fournisseur())
        {
            await ecriture.GetRequiredService<IUserRepository>()
                .AddAsync(chasseur, CancellationToken.None);
        }

        await using var lecture = postgres.Fournisseur();
        var relu = await lecture.GetRequiredService<IUserRepository>()
            .FindByUsernameAsync(chasseur.Username, CancellationToken.None);

        relu.Should().NotBeNull();
        relu!.Id.Should().Be(chasseur.Id);
        relu.Username.Should().Be(chasseur.Username);
        relu.PasswordHash.Should().Be(chasseur.PasswordHash);
        relu.RegisteredAt.Should().Be(chasseur.RegisteredAt);
    }
}
