---
name: tache-suivante
description: Récupère la prochaine tâche ARISE depuis Notion, la passe en "In progress", puis la clôture en "Done" une fois les tests verts et le code committé. Utilise cette skill AU DÉBUT DE CHAQUE SESSION de travail sur ARISE, et dès que l'utilisateur dit "on commence", "on continue", "quelle est la prochaine tâche", "qu'est-ce qu'on fait maintenant", "j'ai fini ça", ou pose une question sur l'avancement — même s'il ne mentionne jamais Notion. Notion est la seule source de vérité des tâches : ne reconstitue jamais une liste de tâches de tête.
---

# Prochaine tâche ARISE (Notion)

Les tâches ne sont pas dupliquées dans le dépôt. Si tu improvises une liste de tâches, tu
travailles sur autre chose que ce que l'utilisateur suit — d'où cette skill.

## 1. Identifier la phase courante

Les phases sont séquentielles : on ne commence pas la 2 avant d'avoir fini la 1 (la phase 1
pose l'auth, le moteur XP/rang et le pattern d'agent que tout le reste réutilise).

La phase courante est **la plus petite phase qui contient encore au moins une tâche non
`Done`**. Commence par la phase 1 ; si elle ne renvoie plus rien, passe à la 2, etc.

Interroge **une seule data source à la fois** : les requêtes multi-sources exigent un plan
Enterprise et échoueront ici.

| Phase | `data_source_url` |
|---|---|
| 1 — Auth + Moteur central + Sport | `collection://47f06b99-4b3d-4a7b-8a60-d72ccf85c839` |
| 2 — Habitudes & Tâches | `collection://0e206557-f306-4093-96d9-c9812bee335e` |
| 3 — Budget | `collection://fb6d8016-8df9-41f7-b48b-3324a443bdd0` |
| 4 — Calendrier + Rapport quotidien | `collection://a69b6a57-54b0-4930-833b-34c88b17cee7` |
| 5 — Extensions de gamification | `collection://1f177934-9111-4eab-a72a-0375d03b79d3` |
| 6 — Systèmes RAG | `collection://8f2b531e-0f32-42be-a204-00ae97ceef5d` |
| 7 — Recommandation courses | `collection://a154734f-5422-4a5d-9e46-c1a2febd168a` |

Schéma identique pour les sept tableaux (vérifié) : `Tâche` (titre), `Catégorie` (`Backend`,
`Frontend`, `Infra`, `Agent IA`, `Tests`), `Statut` (`Not started`, `In progress`, `Done`).

## 2. Lire les tâches restantes

Avec `notion-query-data-sources` en mode SQL — les noms de colonnes sont accentués, garde
les guillemets :

```sql
SELECT url, "Tâche", "Catégorie", "Statut"
FROM "collection://<id-de-la-phase>"
WHERE "Statut" != 'Done'
```

Reprends d'abord toute tâche déjà `In progress`. Par défaut, une seule tâche en cours à la
fois, sinon le tableau ne reflète plus l'état réel — la parallélisation (section 3bis) est
l'exception délibérée, pas la norme, et exige de passer **chaque** tâche lancée en `In progress`
avant de déléguer, jamais une seule pour en cacher deux.

## 3. Choisir la tâche — l'ordre du tableau n'est pas lisible par l'API

**Piège vérifié :** toutes les tâches d'une phase ont été créées en un seul lot et partagent
le même `createdTime`. L'ordre manuel des cartes dans la vue Notion n'est exposé ni par
`createdTime` ni par l'ordre des lignes SQL. Trier par `createdTime` ou prendre la première
ligne renvoyée donne un ordre **arbitraire** : sur la phase 1, la requête renvoie
« Scaffolder la solution » en 6ᵉ position alors que rien ne peut être construit avant.

Choisis donc par **dépendance**, pas par position :

1. `Infra` de fondation (scaffold, `.gitignore`, docker-compose) avant tout code qui doit s'y
   compiler.
2. L'auth avant toute fonctionnalité protégée — règle non négociable du projet.
3. Sens des couches : Domain → Application → Infrastructure → Api → Flutter.
4. `IAgent<TRequest,TResult>` et son faux `HttpMessageHandler` avant tout agent concret.
5. Une tâche `Catégorie: Tests` qui nomme un composant forme **un seul cycle rouge → vert**
   avec la tâche d'implémentation correspondante : traite-les ensemble plutôt que d'écrire un
   test qu'on laisse rouge en repartant ailleurs.

Si deux candidates restent réellement à égalité, propose-les à l'utilisateur et laisse-le
trancher — deviner ici coûte plus cher que demander.

## 3bis. Paralléliser — seulement si les tâches sont vraiment indépendantes

Par défaut, une tâche à la fois. Mais si l'utilisateur demande d'accélérer, ou si plusieurs
candidates de tête de liste (section 3) ne se contredisent sur aucune dépendance, elles
peuvent être menées en parallèle — voir la section « Paralléliser » de l'agent `orchestrateur`
pour la mécanique (worktree par tâche, fusion, revue séparée par tâche).

Deux tâches sont candidates à la parallélisation seulement si **toutes** ces conditions
tiennent :
- Ni l'une ni l'autre ne dépend du résultat de l'autre (vérifie l'ordre de la section 3 :
  une tâche `Backend` qui déclenche une commande d'une autre tâche encore `Not started` n'est
  **pas** indépendante, même si les fichiers ne se recoupent pas encore).
- Elles ne touchent prévisiblement pas les mêmes fichiers — agrégats différents, couches
  différentes, ou pile différente (un backend et un front sur un contrat déjà figé cohabitent
  presque toujours).
- Une tâche `Catégorie: Tests` qui nomme un composant reste couplée à sa tâche d'implémentation
  (règle 5 ci-dessus) — jamais répartie entre deux agents parallèles.

Dans le doute, reste séquentiel : un merge à démêler après coup coûte plus cher que le temps
gagné à paralléliser une hypothèse fausse.

## 4. Passer la tâche en cours

Avant d'écrire la moindre ligne, avec `notion-update-page` :

```json
{ "page_id": "<id extrait de la colonne url>", "command": "update_properties",
  "properties": { "Statut": "In progress" } }
```

## 5. Implémenter

En TDD strict — enchaîne sur la skill `tdd-cqrs`. Si la tâche crée ou modifie un agent
Gemini, enchaîne aussi sur `agent-gemini`.

## 6. Clôturer

Repasse le `Statut` à `Done` **seulement** quand les tests sont verts *et* le code committé.
Une tâche marquée `Done` sur du code non committé fait mentir le tableau à la session
suivante, qui repartira d'une base fausse.

Puis reboucle à l'étape 2 pour la tâche suivante, ou rends la main à l'utilisateur.
