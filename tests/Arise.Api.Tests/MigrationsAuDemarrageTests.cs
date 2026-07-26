using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Arise.Api.Tests;

/// <summary>
/// Le seul test de la suite qui parte d'une base <b>sans schéma</b>. Tout le reste — la
/// collection <see cref="ApiCollection"/> comme <c>PostgresFixture</c> côté Infrastructure —
/// applique les migrations depuis la fixture avant le premier test : c'est précisément cet
/// angle mort qui a laissé la pile <c>docker compose</c> démarrer sur une base vide, où la
/// première requête réelle échouait en <c>42P01: relation "users" does not exist</c>.
///
/// <para>Ce test ne peut donc pas réutiliser <see cref="ApiFixture"/> : elle appelle
/// <c>MigrateAsync</c> elle-même, et le test resterait vert quoi qu'il arrive. Il démarre son
/// propre Postgres jetable, <b>ne le migre pas</b>, et n'affirme qu'une chose : une requête
/// HTTP réelle aboutit. Si l'hôte n'applique pas les migrations en attente à son démarrage,
/// elle ne peut pas aboutir.</para>
///
/// <para>La classe rejoint la collection <see cref="ApiCollection"/> non pour sa fixture, mais
/// pour la sérialisation : la configuration de l'hôte se surcharge par variables
/// d'environnement, qui sont globales au processus. Deux collections en parallèle se
/// voleraient leur chaîne de connexion.</para>
/// </summary>
[Collection(ApiCollection.Nom)]
public sealed class MigrationsAuDemarrageTests : IAsyncLifetime
{
    // Image épinglée, comme les autres fixtures : la suite ne dépend pas de la dernière balise
    // disponible localement.
    private readonly PostgreSqlContainer conteneurVierge =
        new PostgreSqlBuilder("postgres:17-alpine").Build();

    private string? chaineDeConnexionPrecedente;

    public async Task InitializeAsync()
    {
        await conteneurVierge.StartAsync();

        // Aucun MigrateAsync ici — c'est tout l'objet du test.
        chaineDeConnexionPrecedente =
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Postgres", conteneurVierge.GetConnectionString());

        // Program échoue au démarrage sans section Jwt. On repose les mêmes valeurs de test que
        // la fixture de collection : identiques, donc rien à restaurer ensuite.
        Environment.SetEnvironmentVariable("Jwt__Key", ApiFixture.CleJwtDeTest);
        Environment.SetEnvironmentVariable("Jwt__Issuer", ApiFixture.EmetteurDeTest);
        Environment.SetEnvironmentVariable("Jwt__Audience", ApiFixture.AudienceDeTest);
        Environment.SetEnvironmentVariable("Jwt__DureeMinutes", "60");
    }

    public async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Postgres", chaineDeConnexionPrecedente);

        await conteneurVierge.DisposeAsync();
    }

    /// <summary>
    /// Le parcours exact qui échouait en 500 sur la pile locale : première inscription sur un
    /// volume Postgres neuf.
    /// </summary>
    [Fact]
    public async Task Sert_une_premiere_inscription_sur_une_base_jamais_migree()
    {
        // Un hôte neuf, distinct de celui de la collection : il lit la chaîne de connexion
        // posée ci-dessus, donc le conteneur vierge.
        await using var hote = new WebApplicationFactory<Program>();
        var client = hote.CreateClient();

        var reponse = await client.PostAsJsonAsync(
            "/auth/register",
            new { Username = "KaelMorgan", Password = "Ombre-Monarque-2026" });

        reponse.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
