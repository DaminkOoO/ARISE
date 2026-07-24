# CLAUDE.md — ARISE

Ce fichier oriente toute session de travail sur ce dépôt. Lis-le en entier avant de coder.

## Vue d'ensemble

ARISE est une app mobile qui gamifie la vie réelle façon "Solo Leveling" : un profil de
Chasseur progresse en niveau/rang à travers quatre domaines (Sport, Budget, Habitudes &
Tâches, Calendrier), plus des extensions (gamification avancée, RAG recettes/coach sportif,
recommandation de courses). Gemini agit comme "le Système" : génération de quêtes
personnalisées, rapport quotidien, recommandations RAG. **Toute l'interface utilisateur est
en français.**

## État actuel du dépôt

**Scaffold posé, aucun code métier.** La solution `Arise.slnx` existe avec ses quatre projets
`src/` et ses quatre projets de tests, le sens des dépendances Clean Architecture est câblé,
`dotnet build` et `dotnet test` passent. Tout le reste — entités, handlers, agents, écrans —
reste à écrire.

- **`docs/` n'existe pas encore.** Les documents listés dans « Documents de référence » n'y
  sont pas. N'y pars pas à leur recherche ; s'il t'en faut un, demande-le à l'utilisateur
  plutôt que de reconstituer l'architecture de tête.
- **`Arise.Domain` et `Arise.Infrastructure` sont vides de types** — aucun fichier. La
  première entité reste à écrire. `Arise.Application`, en revanche, porte déjà le câblage
  MediatR + FluentValidation (`ValidationBehavior`, `ResolveurNomAffichable`,
  `DependencyInjection`, la sonde de pipeline), et la suite compte **21 tests verts**, tous
  dans `Arise.Application.Tests`. Tu n'écris donc pas le premier test du dépôt : lis les
  tests existants avant d'en ajouter, les conventions y sont déjà posées.
- Le framework cible est **net10.0**, centralisé dans `Directory.Build.props` (et non dans
  chaque `.csproj`). Le changer se fait à un seul endroit.

## Avant toute session de travail : récupérer les tâches depuis Notion

**Ne commence jamais une session sans d'abord consulter le tableau Notion de la phase en
cours.** Les tâches ne sont pas dupliquées dans ce dépôt — Notion est la source de vérité
pour ce qui reste à faire.

Page racine (index de toutes les phases) :
`https://app.notion.com/p/3a4d1fc7022981e4b172fcf546f9a4fc`

Tableaux par phase (utilise l'outil de requête Notion sur ces URLs, filtre `Statut != Done`,
et reprends d'abord toute tâche déjà "In progress") :

⚠️ L'ordre manuel des cartes dans la vue Notion **n'est pas exposé par l'API** : toutes les
tâches d'une phase partagent le même `createdTime` (création en lot) et l'ordre des lignes
SQL est arbitraire. Choisis la tâche par dépendance, pas par position — voir la skill
`tache-suivante`.

| Phase | Tableau Notion |
|---|---|
| 1 — Auth + Moteur central + Sport | `https://app.notion.com/p/aa71df157d224328a69ec34f05f12f7b` |
| 2 — Habitudes & Tâches | `https://app.notion.com/p/a9ace4a550544f2489e2cb771d2144bd` |
| 3 — Budget | `https://app.notion.com/p/8c861202c2014a1bb9f42a497d1ba713` |
| 4 — Calendrier + Rapport quotidien | `https://app.notion.com/p/5d0bcffafb5e406281fe2b870a0afd6e` |
| 5 — Extensions de gamification | `https://app.notion.com/p/38419edc1f30419ab16f056bb448b669` |
| 6 — Systèmes RAG | `https://app.notion.com/p/33c13f07392a4d3e97f57b3c5ac8427d` |
| 7 — Recommandation courses | `https://app.notion.com/p/47c67f14a5ba42d1814403abda3732db` |

**Workflow attendu :**
1. Requête le tableau de la phase courante, identifie la prochaine tâche.
2. Passe son statut à "In progress" avant de commencer.
3. Implémente en TDD strict (voir plus bas).
4. Une fois les tests verts et le code committé, repasse le statut de la tâche à "Done".
5. Ne saute pas de phase — termine la 1 avant de commencer la 2, etc. (la phase 1 pose les
   fondations — auth, moteur XP/rang, pattern d'agent — que toutes les autres réutilisent).

*Nécessite le connecteur MCP Notion configuré dans Claude Code. S'il n'est pas disponible,
demande à l'utilisateur de le connecter avant de continuer plutôt que d'inventer une liste
de tâches.*

## Skills du dépôt (`.claude/skills/`)

Les procédures répétées à chaque session y sont écrites en détail — ce fichier n'en donne que
le principe.

| Skill | Quand |
|---|---|
| `tache-suivante` | Début de session : quelle tâche Notion, et transitions de statut |
| `tdd-cqrs` | Avant toute ligne de code de production : boucle rouge/vert/refactor, conventions CQRS |
| `gamification-domaine` | Moteur central : XP, niveau, rang, séries — formule unique, pièges de dates |
| `persistence-ef` | Repository, DbContext, migration, contrainte, round-trip Testcontainers (vrai Postgres) |
| `agent-gemini` | Dès qu'un agent IA est touché : faux HTTP, validation de la réponse |
| `flutter-riverpod` | Front Flutter/Riverpod : widget test d'abord, tokens HUD, français, frontière backend |
| `garde-fous` | Avant de clôturer une tâche budget / sport / courses / prompt / texte utilisateur |
| `test-fonctionnel-api` | Endpoint ajouté/modifié : couvrir requête → handler → persistance → réponse via un vrai hôte + Postgres jetable |
| `test-fonctionnel-flutter` | Parcours qui traverse l'app réelle (`AriseApp`) plutôt qu'un seul écran isolé |
| `commit-vert` | Au moment de commiter : cadence, message, ce qui n'entre jamais dans l'historique |
| `reprise-git` | Erreur git à rattraper, ou avant toute commande destructive |

## Agents du dépôt (`.claude/agents/`)

Sept agents qui se répartissent trois rôles disjoints : **un décide, deux écrivent, quatre
jugent.** Aucun n'en cumule deux.

| Agent | Rôle |
|---|---|
| `orchestrateur` | Conduit la session : tâche Notion → `codeur`/`codeur-flutter` → les quatre revues → vérifie → `Done` |
| `codeur` | Implémente une tâche backend/.NET déjà choisie, en TDD strict, et commite à chaque état vert |
| `codeur-flutter` | Implémente une tâche front Flutter/Riverpod déjà choisie (catégorie `Frontend`), TDD par widget test |

Les quatre relecteurs sont indépendants et se lancent **en parallèle**, dans un seul message,
avant de passer une tâche en `Done`. Ils lisent les skills correspondantes pour les règles,
ne modifient jamais le code, et rendent un rapport localisé en `fichier:ligne`.

| Relecteur | Question à laquelle il répond |
|---|---|
| `revue-code` | Le code est-il **juste** ? Bugs, cas limites, dates et séries, async, argent, réponses LLM |
| `revue-architecture` | Le code est-il **bien rangé** ? Couches, CQRS, validation, couverture TDD (règles 1-2, 4) |
| `revue-garde-fous` | Le code est-il **sans danger** ? Budget, sport, ton, courses, français (règles 5-7) |
| `revue-commit` | **L'historique** est-il propre ? Secrets, artefacts, gros fichiers, cohérence du lot |

Les quatre angles sont disjoints, et c'est la raison de leur nombre : du code peut être
parfaitement rangé dans la bonne couche, passer tous les garde-fous produit, et calculer le
mauvais niveau de Chasseur. Seul `revue-code` attrape ça.

---

## Pile technique

- **Backend :** ASP.NET Core Web API (**.NET 10**, LTS jusqu'en nov. 2028 — .NET 8 sort de
  support en nov. 2026), Clean Architecture, CQRS via MediatR
- **Base de données :** PostgreSQL + EF Core (extension `pgvector` à partir de la Phase 6)
- **Auth :** nom d'utilisateur/mot de passe maison, `PasswordHasher<T>` autonome, JWT — pas
  de fournisseur d'identité externe
- **IA :** Gemini, appelé via des classes d'agent C# derrière `IAgent<TRequest,TResult>` —
  pas de Semantic Kernel pour l'instant
- **Frontend :** Flutter (mobile, Android d'abord), Riverpod
- **Tests :** xUnit, FluentAssertions, NSubstitute, Testcontainers (Postgres), faux
  `HttpMessageHandler` pour les agents — **jamais d'appel Gemini réel dans la suite de tests**
- **Conteneurisation :** docker-compose (postgres + api + briefing-worker), pas de Kubernetes
- **Scraper courses :** projet Python séparé dans `tools/grocery-scraper/`, job à la demande,
  jamais dans le `docker-compose.yml` principal

## Structure du dépôt

Les projets sont préfixés `Arise.` (`Arise.Domain`, `Arise.Application`, …). Le fichier de
solution est au format **`.slnx`** (XML, format par défaut du SDK .NET 10), pas `.sln`.

```
Arise.slnx
Directory.Build.props   # framework cible + Nullable + ImplicitUsings, communs à tous
├── src/            # Arise.Domain, Arise.Application, Arise.Infrastructure, Arise.Api
├── tests/          # un projet de tests par projet de src/
├── tools/
│   ├── ContentIngestion/     # ingestion des corpus RAG (Phase 6)
│   └── grocery-scraper/      # scraper Python (Phase 7)
├── docs/           # tous les documents de spec — voir liste ci-dessous
└── docker-compose.yml
```

## Commandes

Les commandes .NET ci-dessous ont été exécutées sur ce dépôt et fonctionnent. Celles de
Flutter et Docker restent à vérifier — aucun projet Flutter ni `docker-compose.yml` n'existe
encore.

Backend (.NET, depuis la racine) :

| But | Commande |
|---|---|
| Build | `dotnet build` |
| Toute la suite | `dotnet test` |
| Un projet de test | `dotnet test tests/<Projet.Tests>` |
| Un seul test | `dotnet test --filter "FullyQualifiedName~NomDuTest"` |
| Une classe de tests | `dotnet test --filter "ClassName~NomDeClasse"` |
| Migration EF | `dotnet ef migrations add <Nom> -p src/Arise.Infrastructure -s src/Arise.Infrastructure` |

Flutter : `flutter test`, `flutter test test/<chemin>_test.dart` pour un fichier,
`flutter analyze`, `flutter run`.

Docker : `docker compose up -d postgres`. Les tests Testcontainers démarrent leur propre
instance et n'en dépendent pas — mais Docker doit tourner.

## Documents de référence (place-les dans `docs/`)

- Build prompt principal : architecture C#/CQRS/TDD, catalogue de commandes/requêtes,
  agents, Docker, schéma PostgreSQL
- Additions v2 : quick wins, gamification avancée, systèmes RAG, recommandation de courses
- Brief de design + prompt de correction : tokens visuels (couleurs, typographie Rajdhani/
  Inter/JetBrains Mono, coins HUD, lueurs)
- Écrans additionnels d'onboarding/profil
- Diagramme de classes de référence

Consulte le document pertinent avant d'implémenter une fonctionnalité — ne redevine pas
l'architecture depuis zéro à partir du code existant seul.

## Charte visuelle — « la fenêtre du Système », pas un dashboard SaaS

L'écueil à éviter est un rendu **plat et générique** (« dashboard SaaS sombre »). La structure
et les couleurs ci-dessous sont acquises ; ce qui fait la différence, c'est le **contraste
typographique**, la **signature HUD**, la **lueur** et la **texture de fond**. Tout nouvel écran
respecte ces règles avant toute variation.

### 1. Contraste typographique — trois rôles stricts, jamais une seule police

- **Chiffres et titres** (nom du Chasseur, niveau, XP, valeurs de stats, montants en €) :
  **Rajdhani 700**, letter-spacing 0.5–1px. Visiblement plus imposants et plus « techniques »
  que le reste — augmente la taille si besoin pour créer un vrai contraste.
- **Texte courant** (descriptions de quêtes, messages du Système, corps) : **Inter 400–500**,
  discret, ne concurrence pas les titres.
- **Étiquettes système et métadonnées** (`[RAPPORT DU SYSTÈME]`, `[QUÊTE DU JOUR]`, timestamps,
  labels de stats FOR/VIT/INT/OR/PER) : **JetBrains Mono**, MAJUSCULES, 9–10px, letter-spacing
  1–2px, couleur `--text-dim` (`#8B98A8`).

### 2. Signature HUD — coins en viseur, pas de bordure arrondie

Chaque carte/panneau principal (rapport quotidien, badge de rang, cartes de quêtes/transactions)
porte **4 petits crochets en L** aux coins : ~12–14px, épaisseur 2px, couleur d'accent du
panneau, en léger débord comme un viseur de jeu vidéo. C'est **l'élément signature**, présent sur
**tous** les écrans, pas seulement l'accueil.

### 3. Lueur (glow) réelle — rien ne doit paraître plat

`box-shadow` lumineux (blur 15–25px, opacité ~30–45%, couleur = accent de l'élément) sur : le
badge de rang hexagonal, les barres de stats remplies, la bordure de la carte « Rapport
Quotidien », et tout élément actif de la barre de navigation.

### 4. Texture de fond — grille HUD discrète

Remplace le fond uni par : un **dégradé radial** très subtil (lueur bleue ~10–15% d'opacité)
centré en haut de l'écran, superposé à une **grille fine** (lignes ~1–2% d'opacité, espacées de
~24px). Discret, jamais distrayant.

### Hiérarchie — les éléments « héros » dominent

Nom + rang du Chasseur, carte « Rapport Quotidien », badge hexagonal : **plus grands, plus
lumineux, traitement plus riche** que les éléments de liste (quêtes, transactions, habitudes),
qui restent **compacts et sobres**. Un poids visuel uniforme est ce qui produit l'effet
« template » à bannir.

### Jetons de couleur (acquis — à conserver)

Ces valeurs vivent dans **un seul fichier de thème** (voir la skill `flutter-riverpod`), jamais
codées en dur dans un écran.

| Rôle | Couleur | Note |
|---|---|---|
| Système / neutre | `#3A86FF` | glow `#4CC9F0` |
| Sport | `#E63946` | |
| Budget | `#F2B705` | |
| Habitudes | `#2DC653` | |
| Calendrier | `#9D4EDD` | |
| Fond | `#05070C` | |
| Panneaux | `#0D1420` | bordure `#1E293B` |
| Texte atténué | `#8B98A8` | `--text-dim`, pour les étiquettes JetBrains Mono |

Les **8 écrans** de référence : Accueil, Fenêtre de Statut, Sport, Budget, Habitudes & Tâches,
Calendrier, Onboarding/Éveil, Coach.

## Règles non négociables

1. **TDD strict, sans exception :** rouge → vert → refactor, pour chaque commande, requête,
   entité, agent. Écris le test avant le code de production. Commit à chaque état vert.
2. **CQRS propre :** un handler par commande/requête. Pas de classe "service" qui fait les
   deux.
3. **Auth d'abord :** rien d'autre n'est atteignable sans elle — c'est la première tâche de
   la Phase 1.
4. **Agents IA toujours testés avec un HTTP factice**, jamais contre l'API Gemini réelle en
   CI. Réponse JSON validée avant utilisation, jamais faite confiance aveuglément même avec
   le mode schema.
5. **Garde-fous produit, appliqués en code, pas seulement dans le prompt :**
   - Budget : jamais de conseil investissement/dette/fiscal — nudges comportementaux
     uniquement, catégories limitées à une liste autorisée
   - Sport/RAG coach : jamais de prescription numérique précise, jamais de diagnostic de
     blessure — toujours renvoyer vers un professionnel en cas de douleur
   - Aucune quête manquée ne doit être présentée de façon culpabilisante
6. **Le scraper de courses reste un outil à part**, jamais dans le chemin de requête live de
   l'API — les prix sont des relevés horodatés, jamais présentés comme "en temps réel".
7. **Tout le texte visible par l'utilisateur est en français**, et **au tutoiement** —
   cohérent avec le ton « Système » de Solo Leveling, qui s'adresse au Chasseur directement.
   Le premier message du produit l'a posé (« Ce nom de Chasseur est déjà pris. Choisis-en un
   autre. ») ; ne mélange pas les deux registres d'un écran à l'autre. Le vocabulaire produit
   dit « Chasseur », jamais « utilisateur » ni « pseudo » — l'anglais `Username` reste
   confiné aux identifiants techniques.
