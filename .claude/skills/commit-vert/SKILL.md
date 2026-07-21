---
name: commit-vert
description: La discipline de commit d'ARISE — un commit par état vert, format des messages, et ce qui ne doit jamais entrer dans l'historique (secrets, artefacts de build, gros fichiers). Utilise cette skill dès qu'il s'agit de commiter, de sauvegarder l'avancement, de clôturer un cycle rouge/vert, ou quand l'utilisateur dit "commit", "sauvegarde ça", "j'ai fini", "on enregistre" — en français comme en anglais. Consulte-la aussi avant le tout premier commit du dépôt, où la configuration git et le .gitignore doivent exister au préalable.
---

# Commiter sur ARISE

Le dépôt impose **un commit par état vert**, pas un commit par séance. C'est ce qui rend
chaque étape du TDD révocable : si le cycle suivant part de travers, on revient au dernier
vert sans rien perdre. Un gros commit de fin de session détruit cette propriété.

## Avant le premier commit du dépôt

Trois choses doivent exister, sinon le premier commit échoue ou pollue l'historique pour de
bon :

1. **Une identité git.** `git config user.name` et `user.email` doivent être renseignés,
   sinon `git commit` refuse de s'exécuter. Vérifie-les avant d'annoncer un commit à
   l'utilisateur — c'est le blocage le plus bête et le plus fréquent au démarrage.
2. **Un `.gitignore`.** Le dépôt mêle .NET, Flutter, Python et Docker : sans lui, le premier
   `git add` avale `bin/`, `obj/`, `.dart_tool/`, `build/`, `__pycache__/`, `.venv/` et les
   `*.user`. `dotnet new gitignore` pose une base .NET correcte ; ajoute ensuite les entrées
   Flutter et Python à la main.
3. **Un nom de branche assumé.** Le dépôt est sur `master` (valeur de `init.defaultBranch`).
   Tant qu'il n'y a aucun commit, renommer coûte une commande ; après, ça se négocie avec
   l'historique et le remote. Tranche maintenant.

## Ce qui n'entre jamais dans l'historique

L'historique git est **append-only en pratique** : effacer quelque chose après coup demande
une réécriture, et si le dépôt a été poussé entre-temps, c'est trop tard.

- **La clé d'API Gemini**, et tout secret en général. Elle vit dans la configuration ou les
  variables d'environnement — jamais dans `appsettings.json` versionné, jamais dans un test,
  jamais « temporairement pour essayer ».
- **`appsettings.Development.json`, `.env`, `*.pfx`, `secrets.json`** — ignorés par défaut.
- **Les artefacts de build** : `bin/`, `obj/`, `.dart_tool/`, `build/`, `__pycache__/`.
- **Les gros fichiers de données** : les corpus RAG de la phase 6 et les relevés du scraper
  de la phase 7 n'ont rien à faire dans git. Prévois leur exclusion **avant** de les générer.

Si un secret a déjà été commité, arrête et passe à la skill `reprise-git` : la marche à
suivre commence par révoquer la clé, pas par nettoyer l'historique.

## Cadence et découpage

Un commit = un état vert = un comportement qui marche. Concrètement, à la fin de chaque
cycle rouge → vert → refactor, la suite passe, et tu commites.

Ne mélange pas dans un même commit un comportement nouveau et une reformulation d'autre
chose : quand il faudra revenir en arrière, le commit mixte t'obligera à choisir entre perdre
le correctif et garder le bug.

Lance la suite complète avant de commiter, pas seulement le test ciblé du cycle.

## Format des messages

Conventional Commits, type en anglais (identifiant standard), sujet en français :

```
test(auth): RegisterUserCommand refuse un mot de passe vide
feat(auth): RegisterUserCommandHandler émet un JWT
refactor(sport): extrait le calcul de série de HunterProfile
chore(infra): .gitignore .NET + Flutter
```

Types utilisés ici : `feat`, `test`, `fix`, `refactor`, `chore`, `docs`. La portée est le
domaine (`auth`, `sport`, `budget`, `habitudes`, `calendrier`, `rag`, `courses`, `infra`).

Le sujet décrit **le comportement obtenu**, pas le fichier touché : « ajoute
UserService.cs » ne dit rien à qui relit l'historique dans six mois.

## Lien avec Notion

Le commit est le **préalable** au passage de la tâche en `Done`. L'ordre est : tests verts →
commit → statut `Done`. Marquer `Done` du code non committé fait mentir le tableau, et la
session suivante repart d'une base fausse.

## Pousser

Il n'y a pas encore de remote sur ce dépôt. Quand il y en aura un, ne pousse pas de ta propre
initiative : le commit est réversible localement, la publication ne l'est pas.
