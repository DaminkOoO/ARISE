using Arise.Application.Common.Abstractions;
using Arise.Domain.Users;
using Arise.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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

    // Garde-fou de sécurité central : ce qui atterrit en base est l'empreinte, jamais le
    // secret. On relit la ligne brute — toutes colonnes concaténées — et on exige que le mot
    // de passe en clair n'y figure nulle part.
    [Fact]
    public async Task Ne_stocke_jamais_le_mot_de_passe_en_clair_dans_la_ligne()
    {
        const string motDePasse = "Motdepasse-Ombre-2026";
        var nom = NomUnique("Igris");

        await using var fournisseur = postgres.Fournisseur();
        var empreinte = fournisseur.GetRequiredService<IPasswordHasher>().Hash(motDePasse);
        var chasseur = User.Register(nom, empreinte, Inscrit);
        await fournisseur.GetRequiredService<IUserRepository>().AddAsync(chasseur, CancellationToken.None);

        await using var lecture = postgres.Fournisseur();
        var ligne = await lecture.GetRequiredService<AriseDbContext>().Database
            .SqlQueryRaw<string>(
                "SELECT (id::text || '|' || username || '|' || password_hash || '|' "
                + "|| registered_at::text) AS \"Value\" FROM users WHERE id = {0}",
                chasseur.Id)
            .SingleAsync();

        ligne.Should().NotContain(motDePasse);
    }

    // La collation insensible à la casse doit rendre « Sung » retrouvable par « sung » : sans
    // elle, l'égalité déterministe par défaut de Postgres ne matcherait pas.
    [Fact]
    public async Task Retrouve_un_Chasseur_sans_distinction_de_casse()
    {
        var nom = NomUnique("Sung");
        var chasseur = User.Register(nom, "empreinte-scellée", Inscrit);

        await using (var ecriture = postgres.Fournisseur())
        {
            await ecriture.GetRequiredService<IUserRepository>().AddAsync(chasseur, CancellationToken.None);
        }

        await using var lecture = postgres.Fournisseur();
        var relu = await lecture.GetRequiredService<IUserRepository>()
            .FindByUsernameAsync(nom.ToLowerInvariant(), CancellationToken.None);

        relu.Should().NotBeNull();
        relu!.Id.Should().Be(chasseur.Id);
    }

    [Fact]
    public async Task Confirme_l_existence_sans_distinction_de_casse()
    {
        var nom = NomUnique("Sung");
        var chasseur = User.Register(nom, "empreinte-scellée", Inscrit);

        await using (var ecriture = postgres.Fournisseur())
        {
            await ecriture.GetRequiredService<IUserRepository>().AddAsync(chasseur, CancellationToken.None);
        }

        await using var lecture = postgres.Fournisseur();
        var existe = await lecture.GetRequiredService<IUserRepository>()
            .ExistsWithUsernameAsync(nom.ToLowerInvariant(), CancellationToken.None);

        existe.Should().BeTrue();
    }

    [Fact]
    public async Task Nie_l_existence_d_un_nom_jamais_inscrit()
    {
        await using var lecture = postgres.Fournisseur();

        var existe = await lecture.GetRequiredService<IUserRepository>()
            .ExistsWithUsernameAsync(NomUnique("Absent"), CancellationToken.None);

        existe.Should().BeFalse();
    }

    [Fact]
    public async Task Rend_null_en_cherchant_un_Chasseur_absent()
    {
        await using var lecture = postgres.Fournisseur();

        var relu = await lecture.GetRequiredService<IUserRepository>()
            .FindByUsernameAsync(NomUnique("Absent"), CancellationToken.None);

        relu.Should().BeNull();
    }
}
