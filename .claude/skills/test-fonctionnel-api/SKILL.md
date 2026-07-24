---
name: test-fonctionnel-api
description: Le test fonctionnel bout-en-bout d'ARISE côté backend — vrai hôte API (WebApplicationFactory) par-dessus un vrai Postgres jetable (Testcontainers), requêtes HTTP réelles contre les endpoints, jamais un provider InMemory ni un mock du pipeline. Utilise cette skill dès qu'une tâche ajoute ou modifie un endpoint minimal API (Auth, Sport, Budget, Habitudes, Calendrier…) et qu'il faut couvrir le parcours complet requête → handler → persistance → réponse, en plus des tests unitaires de handler qui restent la base du TDD.
---

# Test fonctionnel API sur ARISE

Les tests unitaires de handler (`Arise.Application.Tests`, NSubstitute sur les repositories)
prouvent que **ta logique** est correcte en isolation — ils restent le socle du TDD, ne les
remplace jamais par ceci. Le test fonctionnel prouve autre chose : que le câblage réel tient
— routage, DI, middlewares (auth, validation), sérialisation JSON, contraintes de base — du
premier octet HTTP jusqu'à la ligne persistée. Un handler parfaitement testé peut rester
inatteignable si l'endpoint n'est jamais mappé, ou si le DI oublie d'enregistrer le repository
: seul un test qui traverse tout le pipeline l'attrape.

**Le patron existe déjà** — ne le réinvente pas. `tests/Arise.Api.Tests/ApiFixture.cs` et
`tests/Arise.Api.Tests/AuthEndpointsTests.cs` sont la référence complète ; lis-les avant
d'écrire quoi que ce soit sur une nouvelle feature.

## Le patron `ApiFixture`

```csharp
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer conteneur = new PostgreSqlBuilder("postgres:17-alpine").Build();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await conteneur.StartAsync();
        // Program lit la config tôt : les surcharges passent en variables d'environnement,
        // exactement comme docker-compose injecte ConnectionStrings__Postgres.
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", conteneur.GetConnectionString());
        // ... section Jwt de test, jamais un secret réel ...

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AriseDbContext>().Database.MigrateAsync();
    }
}

[CollectionDefinition(Nom)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture> { public const string Nom = "Api"; }
```

**Un seul hôte, un seul conteneur par collection de tests**, pas un par test — sinon la suite
rampe. `MigrateAsync`, jamais `EnsureCreated` : on teste le schéma tel qu'il sera déployé, pas
une approximation.

## Une classe de tests par groupe d'endpoints, `[Collection(ApiCollection.Nom)]`

```csharp
[Collection(ApiCollection.Nom)]
public class SportEndpointsTests(ApiFixture api)
{
    [Fact]
    public async Task Complete_une_quete_et_renvoie_le_gain_d_xp()
    {
        var client = api.CreateClient();
        // ... inscription + connexion pour obtenir un jeton, comme AuthEndpointsTests ...
        var reponse = await client.PostAsJsonAsync("/sport/quetes/.../completer", new { });
        reponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

La collection s'exécute **en série** sur une base partagée : chaque test choisit un identifiant
unique (voir `NomUnique` dans `AuthEndpointsTests`) pour ne pas se marcher dessus. Si un
endpoint exige un Chasseur déjà authentifié, passe par `/auth/register` puis `/auth/login` dans
le test lui-même plutôt que de fabriquer un jeton à la main — c'est le parcours réel, et ça
retombe en échec si l'auth se casse ailleurs.

## Ce que ce test couvre, que le test de handler ne couvre pas

- L'endpoint est réellement mappé (une route oubliée dans `Program.cs` fait 404, pas une
  erreur de compilation).
- Le DI résout vraiment toutes les dépendances (`IHunterProfileRepository`, etc.) — une
  interface non enregistrée fait planter le démarrage de l'hôte, pas seulement un test isolé.
- Le contrat JSON réel (noms de champs, casse, formats de date) — un `record` qui sérialise
  différemment de ce que le test unitaire supposait se voit ici.
- Les codes HTTP et l'authentification bout en bout (401/403 sur endpoint protégé, jeton
  mal signé — voir les cas déjà couverts dans `AuthEndpointsTests`).
- Les contraintes réellement posées en base (unicité, non-nullité) via un vrai Postgres —
  voir [[persistence-ef]].

## Ce que ce test ne remplace pas

Les cas limites de logique métier (arithmétique XP, dates de série, validation fine) restent
la responsabilité des tests unitaires de handler et de domaine — un test fonctionnel qui
essaierait de couvrir chaque branche métier via HTTP serait lent et redondant. Un ou deux
tests fonctionnels par endpoint (le chemin heureux, plus le cas d'erreur le plus significatif)
suffisent ; la profondeur des cas limites vit dans `Arise.Application.Tests` et
`Arise.Domain.Tests`.

## Docker doit tourner

Comme pour [[persistence-ef]], Testcontainers démarre son propre Postgres jetable — aucune
instance manuelle requise, mais Docker doit être lancé. Si `dotnet test` échoue sur
`DockerUnavailableException`, ce n'est pas un défaut de ton code : signale-le plutôt que
d'essayer de contourner avec un provider InMemory.
