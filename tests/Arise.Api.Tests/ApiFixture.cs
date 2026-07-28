using Arise.Application.Features.Habits;
using Arise.Application.Features.Hunters;
using Arise.Domain.Habits;
using Arise.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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

    /// <summary>
    /// Remplace les agents du Système par des doublures déterministes.
    ///
    /// <para>Sans cela, l'éveil et les suggestions d'habitudes partiraient joindre l'API Gemini
    /// réelle depuis la suite de tests — ce que la règle non négociable n°4 interdit. Les tests
    /// de bord HTTP éprouvent le parcours et la persistance ; la validation des réponses du
    /// modèle est déjà couverte, à son étage, par les tests d'agent avec faux transport.</para>
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IOnboardingAgent, AgentDOnboardingDeTest>();
            services.AddSingleton<IHabitSuggestionAgent, AgentDeSuggestionDeTest>();
        });

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
/// Narration d'éveil déterministe : le parcours d'éveil s'éprouve sans dépendre de ce qu'un
/// modèle aurait rendu ce jour-là.
/// </summary>
internal sealed class AgentDOnboardingDeTest : IOnboardingAgent
{
    public Task<OnboardingAgentResult> ExecuteAsync(
        OnboardingAgentRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new OnboardingAgentResult(
            "Le Système t'a repéré, Chasseur. Ta voie commence ici.", EstRepli: false));
}

/// <inheritdoc cref="AgentDOnboardingDeTest"/>
internal sealed class AgentDeSuggestionDeTest : IHabitSuggestionAgent
{
    public Task<HabitSuggestionAgentResult> ExecuteAsync(
        HabitSuggestionAgentRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new HabitSuggestionAgentResult(
            [new HabitSuggestion("Marcher après le déjeuner", HabitFrequency.Quotidienne)],
            EstRepli: false));
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
