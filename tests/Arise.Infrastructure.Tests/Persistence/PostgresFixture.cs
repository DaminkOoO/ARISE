using Arise.Application;
using Arise.Infrastructure;
using Arise.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Arise.Infrastructure.Tests.Persistence;

/// <summary>
/// Un seul Postgres jetable, mutualisé par toute la collection : démarrer un conteneur par
/// test ferait ramper la suite. Le schéma est posé par <see cref="RelationalDatabaseFacadeExtensions.MigrateAsync"/>
/// — on éprouve le schéma <b>tel qu'il sera déployé</b>, pas un <c>EnsureCreated</c> qui
/// court-circuiterait les migrations.
///
/// <para>Chaque appel à <see cref="Fournisseur"/> bâtit un provider neuf, donc un
/// <see cref="AriseDbContext"/> neuf : le test de round-trip peut relire depuis un contexte
/// vierge, sans lire le cache d'identité de l'écriture.</para>
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Image épinglée : la suite ne dépend pas de la dernière balise disponible sur la machine.
    private readonly PostgreSqlContainer conteneur =
        new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync()
    {
        await conteneur.StartAsync();

        await using var provider = Fournisseur();
        await provider.GetRequiredService<AriseDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await conteneur.DisposeAsync();

    public ServiceProvider Fournisseur() =>
        new ServiceCollection()
            .AddInfrastructure(conteneur.GetConnectionString())
            .BuildServiceProvider();

    /// <summary>
    /// Les deux couches câblées ensemble, MediatR compris : de quoi éprouver une commande
    /// complète — pipeline, handlers en cascade, abonnés aux événements — par-dessus le vrai
    /// Postgres, là où <see cref="Fournisseur"/> ne monte que la persistance.
    ///
    /// <para>L'horloge est posée avant <c>AddApplication</c>, qui l'enregistre en
    /// <c>TryAddSingleton</c> : celle du test l'emporte, et un scénario daté n'a pas à attendre
    /// minuit.</para>
    /// </summary>
    public ServiceProvider FournisseurApplicatif(TimeProvider horloge) =>
        new ServiceCollection()
            .AddSingleton(horloge)
            .AddApplication()
            .AddInfrastructure(conteneur.GetConnectionString())
            .BuildServiceProvider();
}

/// <summary>
/// Rassemble tous les tests d'intégration sous un même conteneur. Une collection s'exécute en
/// série : les tests peuvent partager la base sans se marcher dessus, à condition de choisir
/// des noms de Chasseur distincts.
/// </summary>
[CollectionDefinition(Nom)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Nom = "Postgres";
}
