using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Arise.Infrastructure.Persistence;

/// <summary>
/// Applique les migrations en attente au démarrage de l'hôte.
///
/// <para>Le schéma vit ici, dans Infrastructure, et non dans <c>Program.cs</c> : l'API appelle
/// <see cref="DependencyInjection.AddInfrastructure"/> et rien d'autre, elle ne connaît ni EF
/// Core ni Npgsql. Sortir <c>AriseDbContext</c> jusqu'à la couche API pour l'appel à
/// <c>Migrate</c> aurait percé cette frontière pour une seule ligne.</para>
/// </summary>
public static class MigrationsDeDemarrage
{
    /// <summary>
    /// <para><b>Pourquoi au démarrage de l'API</b> — et non dans une étape distincte (job
    /// d'init, <c>dotnet ef database update</c> dans l'image, bundle de migrations) : ce dépôt
    /// n'a pas d'environnement de production réel, et <c>docker-compose.yml</c> se décrit
    /// lui-même comme la pile locale. Une étape séparée demanderait le SDK dans l'image
    /// d'exécution ou un conteneur d'init supplémentaire, pour un bénéfice qui n'existe qu'avec
    /// un vrai déploiement. Le jour où celui-ci existera, c'est ce point qu'il faudra
    /// reprendre — pas le reste du câblage.</para>
    ///
    /// <para><b>Pourquoi sans condition d'environnement</b> : migrer automatiquement en
    /// production est une pratique discutée à raison (plusieurs instances qui démarrent en même
    /// temps, migration destructive appliquée sans relecture, droits DDL accordés au compte
    /// applicatif). Mais la conditionner à <c>IsDevelopment()</c> ne corrigerait précisément
    /// rien ici : <c>docker-compose.yml</c> ne pose pas <c>ASPNETCORE_ENVIRONMENT</c>, la pile
    /// tourne donc en <c>Production</c> — l'environnement même qu'on aurait exclu. Le garde-fou
    /// serait aussi invisible du test fonctionnel, qui boote dans un autre environnement : vert
    /// à la CI, cassé sur la pile réelle. On migre donc toujours, et l'on assume le point de
    /// reprise ci-dessus plutôt qu'une condition qui donne l'illusion d'une précaution.</para>
    ///
    /// <para><b>Pourquoi aucune temporisation ni reprise</b> : le service <c>api</c> attend
    /// <c>condition: service_healthy</c>, la base est donc joignable quand cette ligne
    /// s'exécute — une attente ajoutée ici ne couvrirait rien que le healthcheck ne couvre
    /// déjà. Le réflexe voisin, <c>EnableRetryOnFailure</c>, serait par ailleurs nuisible :
    /// la stratégie d'exécution de Npgsql refuse les transactions ouvertes à la main, or
    /// <c>EfUnitOfWork</c> appelle <c>BeginTransactionAsync</c> à chaque commande. L'activer
    /// pour sécuriser le démarrage casserait <c>TransactionBehavior</c> sur toutes les
    /// requêtes. À défaut de reprise, un échec ici arrête l'hôte bruyamment — ce qui vaut mieux
    /// que de servir des requêtes sur un schéma absent, le défaut qu'on corrige.</para>
    /// </summary>
    public static async Task AppliquerLesMigrationsEnAttenteAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        // Un scope : AriseDbContext est enregistré scoped, le provider racine ne le résout pas.
        await using var scope = services.CreateAsyncScope();

        await scope.ServiceProvider
            .GetRequiredService<AriseDbContext>()
            .Database
            // MigrateAsync, jamais EnsureCreated : on pose le schéma tel que les migrations le
            // décrivent, celui-là même qu'éprouvent PostgresFixture et ApiFixture.
            .MigrateAsync(cancellationToken);
    }
}
