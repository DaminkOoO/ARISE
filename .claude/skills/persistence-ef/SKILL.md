---
name: persistence-ef
description: Les conventions de persistance d'ARISE — EF Core, migrations, mapping snake_case, repositories qui implémentent les interfaces d'Application, et tests d'intégration sur un vrai Postgres via Testcontainers (jamais le provider InMemory). Utilise cette skill dès qu'une tâche touche un repository, le DbContext, une migration, une configuration d'entité, une contrainte de base, ou un test de round-trip persistance. Ne teste jamais EF Core lui-même : teste ton mapping, ton repository et ta requête.
---

# Persistance EF Core sur ARISE

EF Core est une **dépendance**, pas ton code. Tu ne le testes pas ; tu testes ce que *tu*
écris par-dessus : le mapping d'une entité, une contrainte, un repository, une requête. Le
socle est déjà posé — `AriseDbContext` mappe en snake_case via `EFCore.NamingConventions`,
la migration initiale des Chasseurs existe, et un test anti-oubli garde le modèle et les
migrations synchronisés. Ne le refais pas : lis l'existant avant d'ajouter.

## Le mapping vit dans une `IEntityTypeConfiguration`, pas dans `OnModelCreating`

Une classe de configuration par entité (`UserConfiguration` est le modèle), appliquée via
`ApplyConfigurationsFromAssembly`. C'est ce qui garde `OnModelCreating` lisible quand les
sept phases auront ajouté leurs tables. Les contraintes métier — unicité du nom de Chasseur,
non-nullité, longueurs — se déclarent là, en base, pas seulement en validation applicative :
un validator se contourne par un second chemin d'écriture, une contrainte `UNIQUE` non.

## Une migration à chaque changement de modèle, dans le même état vert

Le dépôt porte déjà un **test anti-oubli** (`MigrationsTests`) qui rougit si le modèle diffère
de la dernière migration. Donc : dès que tu changes une entité ou une configuration, génère la
migration dans le même cycle, avant de commiter. Ne laisse jamais un modèle en avance sur ses
migrations « le temps de finir » — le prochain `dotnet ef` ou le prochain démarrage part d'une
base fausse.

```
dotnet ef migrations add <Nom> -p src/Arise.Infrastructure -s src/Arise.Api
```

Nomme la migration par l'intention métier en français (`CreationDesChasseurs`), pas
`Migration1`.

## Les repositories implémentent les interfaces d'Application

L'interface (`IUserRepository`) est déclarée dans **Application** ; l'implémentation EF vit
dans **Infrastructure** et est câblée dans son `DependencyInjection`. Le repository rend des
**types du domaine**, jamais un `IQueryable`, jamais une entité EF nue qui fuirait le tracking
et le schéma vers la couche métier. Si un `IQueryable` traverse la frontière, Application se
met à dépendre du provider — la couche n'est plus étanche.

Un `SaveChangesAsync` par intention (par commande), pas un par ligne. Sois explicite sur le
tracking : une lecture pure passe en `AsNoTracking`.

## Les tests d'intégration tournent sur un vrai Postgres (Testcontainers)

Pas le provider **InMemory** : il ne connaît ni les contraintes `UNIQUE`, ni la casse, ni les
types Postgres, ni les transactions — il rend verts des tests qui mentent. Testcontainers
démarre un Postgres jetable ; **Docker doit tourner**, mais aucune instance manuelle n'est
requise.

- Mutualise le conteneur avec une `IAsyncLifetime` + `ICollectionFixture` : un conteneur par
  collection de tests, pas un par test — sinon la suite rampe.
- Applique les migrations au démarrage du conteneur (`Database.MigrateAsync`), pas
  `EnsureCreated` : on teste le schéma **tel qu'il sera déployé**.
- Le test de round-trip écrit puis relit depuis un **nouveau** `DbContext`, pour ne pas lire
  le cache d'identité de l'écriture.

## Round-trip du hachage : le mot de passe n'est jamais lisible en base

Pour la tâche `EfUserRepository + PasswordHasher`, le test qui compte vérifie qu'après
persistance **le mot de passe en clair n'apparaît nulle part** dans la ligne — seul le hash y
est. Persiste, relis dans un contexte neuf, et affirme que le hash vérifie le bon mot de passe
et rejette un mauvais. C'est un garde-fou de sécurité, pas un détail de mapping.

## Ce que tu ne testes pas

Ni EF Core, ni Npgsql, ni la génération de migrations : ce sont des dépendances. Un test qui
affirme « `SaveChanges` sauvegarde » teste Microsoft. Teste **ta** contrainte, **ton**
repository, **ton** round-trip.

Voir [[tdd-cqrs]] pour la boucle, [[commit-vert]] pour ne pas noyer une migration dans un
commit fourre-tout.
