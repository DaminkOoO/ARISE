using System.Net;
using System.Net.Http.Headers;
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

    [Fact]
    public async Task Connecte_un_Chasseur_avec_les_bons_identifiants()
    {
        var client = api.CreateClient();
        var nom = NomUnique("Sung");
        await client.PostAsJsonAsync("/auth/register", new { Username = nom, Password = MotDePasse });

        var reponse = await client.PostAsJsonAsync(
            "/auth/login",
            new { Username = nom, Password = MotDePasse });

        reponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Remet_un_jeton_non_vide_a_la_connexion()
    {
        var client = api.CreateClient();
        var nom = NomUnique("Cha");
        await client.PostAsJsonAsync("/auth/register", new { Username = nom, Password = MotDePasse });

        var reponse = await client.PostAsJsonAsync(
            "/auth/login",
            new { Username = nom, Password = MotDePasse });

        var corps = await reponse.Content.ReadFromJsonAsync<ReponseLogin>();
        corps!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Date_l_expiration_du_jeton_dans_le_futur()
    {
        var client = api.CreateClient();
        var nom = NomUnique("Yoo");
        await client.PostAsJsonAsync("/auth/register", new { Username = nom, Password = MotDePasse });

        var reponse = await client.PostAsJsonAsync(
            "/auth/login",
            new { Username = nom, Password = MotDePasse });

        var corps = await reponse.Content.ReadFromJsonAsync<ReponseLogin>();
        corps!.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Refuse_un_mauvais_mot_de_passe_en_401()
    {
        var client = api.CreateClient();
        var nom = NomUnique("Jin");
        await client.PostAsJsonAsync("/auth/register", new { Username = nom, Password = MotDePasse });

        var reponse = await client.PostAsJsonAsync(
            "/auth/login",
            new { Username = nom, Password = "mauvais-mot-de-passe" });

        reponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Donne_l_identite_du_Chasseur_courant_avec_un_jeton()
    {
        var client = api.CreateClient();
        var nom = NomUnique("Sung");
        await client.PostAsJsonAsync("/auth/register", new { Username = nom, Password = MotDePasse });
        var connexion = await client.PostAsJsonAsync("/auth/login", new { Username = nom, Password = MotDePasse });
        var jeton = (await connexion.Content.ReadFromJsonAsync<ReponseLogin>())!.AccessToken;

        var requete = new HttpRequestMessage(HttpMethod.Get, "/auth/moi");
        requete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jeton);
        var reponse = await client.SendAsync(requete);

        reponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Rend_le_nom_du_Chasseur_authentifie()
    {
        var client = api.CreateClient();
        var nom = NomUnique("Thomas");
        await client.PostAsJsonAsync("/auth/register", new { Username = nom, Password = MotDePasse });
        var connexion = await client.PostAsJsonAsync("/auth/login", new { Username = nom, Password = MotDePasse });
        var jeton = (await connexion.Content.ReadFromJsonAsync<ReponseLogin>())!.AccessToken;

        var requete = new HttpRequestMessage(HttpMethod.Get, "/auth/moi");
        requete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jeton);
        var reponse = await client.SendAsync(requete);

        var corps = await reponse.Content.ReadFromJsonAsync<ReponseMoi>();
        corps!.Username.Should().Be(nom);
        // L'Id porté par le claim « sub » remonte bien, non vide : un claim mal relu retomberait
        // sur Guid.Empty (ou lèverait) plutôt que sur l'identité réelle du Chasseur.
        corps.UserId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Refuse_l_endpoint_protege_sans_jeton_en_401()
    {
        var client = api.CreateClient();

        var reponse = await client.GetAsync("/auth/moi");

        reponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Chemin le plus sensible côté sécurité : un nom jamais inscrit ne doit pas se distinguer
    // d'un mauvais mot de passe. Même 401, sans dire lequel des deux champs est en cause.
    [Fact]
    public async Task Refuse_un_nom_de_Chasseur_inconnu_en_401()
    {
        var client = api.CreateClient();

        var reponse = await client.PostAsJsonAsync(
            "/auth/login",
            new { Username = NomUnique("Fantome"), Password = MotDePasse });

        reponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Un jeton présent mais mal signé (signature cassée, ou signé d'une autre clé) doit être
    // rejeté, pas seulement un jeton absent : c'est ce sens qui attrape une divergence de
    // configuration entre l'émetteur et le valideur.
    [Fact]
    public async Task Refuse_l_endpoint_protege_avec_un_jeton_mal_signe_en_401()
    {
        var client = api.CreateClient();
        var nom = NomUnique("Sung");
        await client.PostAsJsonAsync("/auth/register", new { Username = nom, Password = MotDePasse });
        var connexion = await client.PostAsJsonAsync("/auth/login", new { Username = nom, Password = MotDePasse });
        var jeton = (await connexion.Content.ReadFromJsonAsync<ReponseLogin>())!.AccessToken;

        // Corps du jeton intact, signature cassée : les quatre derniers caractères remplacés.
        var altere = jeton[..^4] + "XXXX";
        var requete = new HttpRequestMessage(HttpMethod.Get, "/auth/moi");
        requete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", altere);

        var reponse = await client.SendAsync(requete);

        reponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record ReponseLogin(string AccessToken, DateTimeOffset ExpiresAt);

    private sealed record ReponseMoi(Guid UserId, string Username);
}
