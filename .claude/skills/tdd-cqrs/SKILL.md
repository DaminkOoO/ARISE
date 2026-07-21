---
name: tdd-cqrs
description: La boucle TDD stricte (rouge → vert → refactor → commit) et les conventions CQRS/MediatR d'ARISE. Utilise cette skill AVANT d'écrire la moindre ligne de code de production sur ce projet — dès qu'il s'agit de créer ou modifier une commande, une requête, un handler, un validator, une entité du domaine, un repository ou un endpoint d'API, même pour un changement qui paraît trivial. Le TDD strict est une règle non négociable du dépôt : le test s'écrit toujours en premier.
---

# TDD strict + CQRS sur ARISE

Le TDD n'est pas une préférence de style ici : c'est la règle n°1 du dépôt. Écrire le code de
production d'abord puis « ajouter les tests » produit des tests qui décrivent ce que le code
fait, pas ce qu'il devrait faire — et le bug passe.

## La boucle

**Rouge** — écris le test, exécute-le, vois-le échouer.

En C#, le premier rouge d'un composant neuf est presque toujours une **erreur de
compilation** (la classe n'existe pas encore). C'est un rouge légitime, mais il ne prouve
rien sur l'assertion. Crée donc le strict minimum pour que ça compile (classe vide, méthode
qui `throw new NotImplementedException()`), relance, et vérifie que l'échec vient maintenant
de **l'assertion** — c'est ce rouge-là qui valide que le test teste réellement quelque chose.

**Vert** — le code le plus simple qui fait passer le test. Pas la généralisation que tu
anticipes : celle-là viendra avec le test qui l'exige.

**Refactor** — nettoie à tests verts, relance après.

**Commit** — à chaque état vert, pas en fin de session. C'est ce qui rend chaque étape
révocable. Cadence, format des messages et ce qui ne doit jamais entrer dans l'historique :
voir la skill `commit-vert`.

## CQRS : un handler, une intention

Une commande **écrit** et renvoie le minimum. Une requête **lit** et ne modifie rien. Un
handler par commande/requête, et pas de classe « service » qui ferait les deux — c'est la
règle n°2 du dépôt. Quand une commande doit en déclencher une autre (`CompleteGymQuest` →
`AwardXp`), passe par MediatR ou par un événement de domaine, jamais par un appel direct de
handler à handler : le handler appelé perd sinon sa validation et son pipeline.

La validation vit dans un `FluentValidation` validator branché sur le `ValidationBehavior`
du pipeline, pas dans le handler.

## Où vont les fichiers

Le préfixe des projets (`Arise.Domain` ou `Domain`) est fixé au scaffold — adapte les chemins
ci-dessous à ce qui existe réellement une fois la solution créée.

```
src/Arise.Application/Features/<Domaine>/Commands/<Nom>/
    <Nom>Command.cs  <Nom>CommandHandler.cs  <Nom>CommandValidator.cs
tests/Arise.Application.Tests/Features/<Domaine>/Commands/<Nom>CommandHandlerTests.cs
```

Le test miroite l'arborescence du code testé — on retrouve ainsi le test depuis le fichier
et inversement sans chercher.

## Outillage

- **xUnit** + **FluentAssertions** pour les assertions (`resultat.Should().Be(...)`).
- **NSubstitute** pour les doublures — jamais pour ce que tu peux instancier réellement ;
  une entité du domaine se construit, elle ne se mocke pas.
- **Testcontainers (Postgres)** pour tout ce qui touche EF Core et les repositories. Docker
  doit tourner ; les tests démarrent leur propre instance et ne dépendent pas du
  `docker compose up` local.

## Commandes

| But | Commande |
|---|---|
| Toute la suite | `dotnet test` |
| Un projet | `dotnet test tests/<Projet.Tests>` |
| Une classe | `dotnet test --filter "ClassName~NomDeClasse"` |
| Un test | `dotnet test --filter "FullyQualifiedName~NomDuTest"` |

Pendant la boucle, lance le test ciblé (`--filter`) — c'est ce qui rend le cycle assez rapide
pour être tenu. Passe la suite complète avant de commiter.

## Pièges

- Un test qui n'a jamais été rouge ne prouve rien. Si tu écris le test après coup, tu ne
  sauras pas s'il échoue quand il le devrait.
- Un test par comportement. Six assertions dans un test : au premier échec tu ne sais pas
  lequel des six comportements est cassé.
- Ne teste pas EF Core ni MediatR — ce sont des dépendances. Teste **ta** logique.
- Tout texte destiné à l'utilisateur est en français, y compris les messages d'erreur de
  validation — ils remontent jusqu'à l'écran.
