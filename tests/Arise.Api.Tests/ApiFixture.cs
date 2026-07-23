using Arise.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Arise.Api.Tests;

/// <summary>
/// Boot le vrai hôte de l'API par-dessus un Postgres jetable (Testcontainers) : le parcours
/// d'auth est éprouvé de bout en bout, du endpoint HTTP jusqu'à la ligne persistée, jamais sur
/// un provider InMemory qui ignore contraintes et collation.
///
/// <para>La configuration réelle est surchargée ici : chaîne de connexion du conteneur et
/// section <c>Jwt</c> de test. La clé JWT est un jeton de test explicite, pas un secret de
/// production — aucune clé réelle n'entre dans le dépôt.</para>
/// </summary>
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Image épinglée : la suite ne dépend pas de la dernière balise disponible localement.
    private readonly PostgreSqlContainer conteneur =
        new PostgreSqlBuilder("postgres:17-alpine").Build();

    // ≥ 256 bits, requis par HS256. Valeur de test assumée, sans valeur hors de cette suite.
    public const string CleJwtDeTest =
        "clé-de-test-arise-hmac-sha256-au-moins-256-bits-de-longueur-suffisante";

    public const string EmetteurDeTest = "arise-tests";

    public const string AudienceDeTest = "arise-clients-tests";

    async Task IAsyncLifetime.InitializeAsync()
    {
        await conteneur.StartAsync();

        // Program lit la configuration tôt (chaîne de connexion, section Jwt) avant même de
        // bâtir l'hôte : les surcharges doivent donc être visibles dès la première ligne. On
        // les pose en variables d'environnement, exactement comme docker-compose injecte
        // ConnectionStrings__Postgres — c'est le chemin de config réellement déployé.
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", conteneur.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Key", CleJwtDeTest);
        Environment.SetEnvironmentVariable("Jwt__Issuer", EmetteurDeTest);
        Environment.SetEnvironmentVariable("Jwt__Audience", AudienceDeTest);
        Environment.SetEnvironmentVariable("Jwt__DureeMinutes", "60");

        // Le schéma est posé tel qu'il sera déployé (MigrateAsync, pas EnsureCreated).
        // Accéder à Services bâtit l'hôte, donc lit la config posée ci-dessus.
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AriseDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", null);
        Environment.SetEnvironmentVariable("Jwt__Key", null);
        Environment.SetEnvironmentVariable("Jwt__Issuer", null);
        Environment.SetEnvironmentVariable("Jwt__Audience", null);
        Environment.SetEnvironmentVariable("Jwt__DureeMinutes", null);

        await conteneur.DisposeAsync();
        await base.DisposeAsync();
    }
}

/// <summary>
/// Un seul hôte et un seul conteneur pour tout le parcours d'auth : la collection s'exécute en
/// série, chaque test choisit un nom de Chasseur unique pour ne pas se marcher dessus.
/// </summary>
[CollectionDefinition(Nom)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Nom = "Api";
}
