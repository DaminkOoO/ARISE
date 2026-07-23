---
name: flutter-riverpod
description: Les conventions du front Flutter/Riverpod d'ARISE — TDD par widget test, état via Riverpod, interface intégralement en français au tutoiement, tokens visuels HUD tirés du design (jamais inventés en ligne), et frontière nette avec le backend. Utilise cette skill dès qu'une tâche touche un écran, un widget, un provider Riverpod, la navigation, le stockage du JWT côté app, ou tout affichage destiné au Chasseur. Le premier écran scaffolde le projet Flutter, qui n'existe pas encore.
---

# Front Flutter / Riverpod sur ARISE

Le mobile (Android d'abord) affiche le Système ; il ne le calcule pas. Toute la logique de
progression — XP, niveau, rang, séries — vit côté backend (voir [[gamification-domaine]]).
L'app **montre** l'état que l'API renvoie ; elle ne recalcule jamais un niveau ni un rang en
Dart. Dupliquer la formule ici, c'est se garantir deux vérités divergentes le jour d'un
ajustement de barème.

**Aucun projet Flutter n'existe encore.** La première tâche front le scaffolde. Avant, établis
l'arborescence réelle ; ne travaille pas de mémoire.

## TDD aussi ici : le widget test s'écrit d'abord

La règle n°1 du dépôt ne s'arrête pas au C#. Un comportement d'écran se décrit par un test
avant d'exister :

- **Widget test** (`flutter_test`, `WidgetTester`) pour ce que voit et fait le Chasseur : un
  état de chargement s'affiche, une erreur montre le bon message français, un tap déclenche la
  bonne action.
- **Test de provider / notifier** pour la logique d'état, isolée de l'UI.

Tu ne testes pas Flutter ni Riverpod eux-mêmes — tu testes **ton** écran, **ton** notifier,
**ton** câblage. `flutter test` pour la suite, `flutter test test/<chemin>_test.dart` pour un
fichier, `flutter analyze` avant de commiter (traite les warnings comme des erreurs).

## Riverpod : l'UI est bête, l'état est dans les providers

La logique d'état vit dans des `Notifier` / `AsyncNotifier` exposés par des providers ; le
widget lit et affiche. Un widget qui appelle l'API directement et garde de l'état local mutable
n'est ni testable ni réutilisable. Les appels réseau passent par un provider de service
injectable — qu'un test remplace par un double, exactement comme le faux `HttpMessageHandler`
côté backend : **jamais d'appel réseau réel dans la suite**.

Chaque écran adossé à des données traite explicitement ses **trois états** : chargement,
donnée, erreur. Un `AsyncValue` non déballé qui affiche un écran blanc pendant le chargement
est un bug, pas un détail.

## Français, tutoiement, vocabulaire « Chasseur »

Tout texte visible est en français et **au tutoiement**, cohérent avec le ton Système (règle 7
du dépôt, voir [[garde-fous]]). On dit « Chasseur », jamais « utilisateur » ni « pseudo ». Une
série rompue, une quête manquée ne sont **jamais** formulées de façon culpabilisante. N'écris
pas de chaîne anglaise « temporaire » : elle finit à l'écran.

Externalise les libellés (un fichier de chaînes) plutôt que de les semer dans les widgets — la
relecture du ton se fait à un seul endroit.

## Les tokens visuels viennent du design, pas de ton clavier

Le brief de design fixe les couleurs, la typographie (Rajdhani / Inter / JetBrains Mono), les
coins HUD et les lueurs. Ces valeurs vivent dans **un seul `ThemeData` / fichier de tokens**,
et chaque écran s'y réfère — jamais un `Color(0xFF…)` ni une taille de police codés en ligne
dans un widget. Un hex en dur dans un écran, c'est le token qui diverge au troisième écran.

**Tu n'inventes pas ces valeurs.** La charte est désormais dans `CLAUDE.md` (section « Charte
visuelle ») : trois rôles typographiques stricts (Rajdhani 700 pour chiffres/titres, Inter pour
le corps, JetBrains Mono majuscules pour les étiquettes), coins en viseur HUD, lueurs réelles,
grille de fond, et les jetons de couleur par domaine. Lis-la avant de poser le moindre style —
l'écueil documenté est le rendu « dashboard SaaS » plat et générique. Ce qui n'y figure pas
(une valeur précise absente, un écran non couvert) se **demande**, ne se reconstitue pas.

## Auth : le JWT se stocke en sécurisé et voyage avec chaque appel

Le jeton émis par le backend se range dans `flutter_secure_storage`, pas dans un
`SharedPreferences` en clair. Un intercepteur l'attache aux appels API et gère le **401** :
jeton absent ou expiré → retour à la connexion, sans écran cassé. Teste ce chemin — c'est la
porte d'entrée de tout le reste (règle 3 : auth d'abord).

## Structure

Les dossiers front miroitent les features du backend (`auth`, `sport`, …) pour qu'on passe de
l'écran au handler sans chercher. Voir [[commit-vert]] pour la cadence : un commit par état
vert vaut aussi pour un widget test.
