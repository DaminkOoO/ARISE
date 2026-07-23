using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Arise.Api.Tests;

/// <summary>
/// Parcours d'auth de bout en bout : inscription, connexion, endpoint protégé avec et sans
/// jeton. Un comportement par test ; chaque test choisit un nom de Chasseur unique, la
/// collection partageant une base et son index unique.
/// </summary>
[Collection(ApiCollection.Nom)]
public class AuthEndpointsTests(ApiFixture api)
{
    // Le nom d'un Chasseur est plafonné à 32 : un suffixe court distingue les tests.
    private static string NomUnique(string racine) => $"{racine}{Guid.NewGuid():N}"[..12];

    private const string MotDePasse = "Ombre-Monarque-2026";

    [Fact]
    public async Task Inscrit_un_nouveau_Chasseur()
    {
        var client = api.CreateClient();

        var reponse = await client.PostAsJsonAsync(
            "/auth/register",
            new { Username = NomUnique("Sung"), Password = MotDePasse });

        reponse.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Refuse_une_inscription_sur_un_nom_deja_pris_en_409()
    {
        var client = api.CreateClient();
        var nom = NomUnique("Igris");

        await client.PostAsJsonAsync("/auth/register", new { Username = nom, Password = MotDePasse });

        var reponse = await client.PostAsJsonAsync(
            "/auth/register",
            new { Username = nom, Password = MotDePasse });

        reponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Explique_le_conflit_de_nom_en_francais()
    {
        var client = api.CreateClient();
        var nom = NomUnique("Beru");

        await client.PostAsJsonAsync("/auth/register", new { Username = nom, Password = MotDePasse });

        var reponse = await client.PostAsJsonAsync(
            "/auth/register",
            new { Username = nom, Password = MotDePasse });

        var corps = await reponse.Content.ReadAsStringAsync();
        corps.Should().Contain("Ce nom de Chasseur est déjà pris");
    }

    [Fact]
    public async Task Refuse_une_inscription_invalide_en_400()
    {
        var client = api.CreateClient();

        var reponse = await client.PostAsJsonAsync(
            "/auth/register",
            new { Username = NomUnique("Tank"), Password = "" });

        reponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
