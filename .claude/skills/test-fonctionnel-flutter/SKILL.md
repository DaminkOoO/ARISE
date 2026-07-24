---
name: test-fonctionnel-flutter
description: Le test fonctionnel bout-en-bout d'ARISE côté Flutter — package integration_test officiel, l'app réelle (AriseApp depuis main.dart) montée en entier plutôt qu'un seul écran isolé, backend simulé (jamais d'appel réseau réel). Utilise cette skill quand une tâche doit couvrir un parcours qui traverse plusieurs écrans ou vérifier que le câblage réel de main.dart (thème, ProviderScope racine, polices) fonctionne, en plus des widget tests par écran qui restent la base du TDD front.
---

# Test fonctionnel Flutter sur ARISE

Les widget tests par écran (`test/features/<domaine>/<ecran>_screen_test.dart`) montent **un
seul widget** (ex. `AuthScreen`) sous un `ProviderScope` de test — ils restent le socle du TDD
front, ne les remplace jamais par ceci. Ils ne prouvent pas que `main.dart` assemble
correctement l'app réelle : un provider oublié dans le vrai `ProviderScope` racine, un thème
mal appliqué, une police qui échoue à charger — rien de tout ça n'est visible en montant les
écrans un par un. Le test fonctionnel monte **`AriseApp` telle qu'elle démarre en production**.

## Où ça vit

`app/integration_test/`, à côté de `app/test/` (convention du package `integration_test`, pas
un sous-dossier de `test/`). Un fichier par parcours, ex. `integration_test/accueil_test.dart`.

## Dépendance

```yaml
dev_dependencies:
  integration_test:
    sdk: flutter
```

Package officiel du SDK Flutter — pas une dépendance externe à choisir, elle est déjà la
convention standard pour ce genre de test.

## Le patron : monter `AriseApp`, pas un écran

```dart
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:integration_test/integration_test.dart';
import 'package:arise/main.dart';
import 'package:arise/features/accueil/home_providers.dart';

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('démarre et affiche l\'accueil avec les données du Chasseur', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [homeServiceProvider.overrideWithValue(_FakeHomeService())],
        child: const AriseApp(),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Rapport du Système'), findsOneWidget);
  });
}
```

`AriseApp` (dans `lib/main.dart`) n'ouvre pas son propre `ProviderScope` — le `runApp` de
production en pose un à la racine. Le test fait pareil, mais avec des overrides : ça exerce le
**vrai** `MaterialApp`, le **vrai** `ThemeData`, la **vraie** composition de `main.dart`, avec
seulement le service réseau remplacé par un double — jamais un appel HTTP réel, exactement la
même règle que pour les widget tests et les agents backend.

**Commande exacte sur ce dépôt :** `flutter test -d flutter-tester integration_test/`. Ni
`windows/` ni `web/` ne sont scaffoldés dans `app/` (Android seulement) — sans `-d
flutter-tester`, la commande échoue avec « No supported devices connected », un faux rouge sans
rapport avec le test lui-même. `flutter test` seul (sans chemin) ne ramasse **pas**
`integration_test/` : lance les deux commandes séparément (`flutter test` pour `test/`,
`flutter test -d flutter-tester integration_test/` pour les fonctionnels).

## `IntegrationTestWidgetsFlutterBinding`, pas `TestWidgetsFlutterBinding`

Le binding du package `integration_test` remplace celui utilisé dans `test/support/fonts.dart`
(`TestWidgetsFlutterBinding.ensureInitialized()`) — c'est ce qui permet au test de tourner à la
fois en mode hôte (`flutter test integration_test/…`, rapide, pas d'émulateur requis pour cette
suite) et sur un vrai appareil (`flutter test integration_test/… -d <device>`) sans rien
changer au fichier de test.

## Ce que ce test couvre, que le widget test par écran ne couvre pas

- `main.dart` assemble réellement l'app : le `ProviderScope` racine, le `MaterialApp`, le
  thème (`ariseTheme()`) sont exercés pour de vrai, pas reconstitués à la main dans le test.
- Le chargement des polices embarquées (Rajdhani/Inter/JetBrains Mono) ne lève pas d'exception
  au démarrage réel.
- Le parcours entre plusieurs écrans, **une fois qu'un vrai routeur existe** (voir plus bas —
  ce n'est pas encore le cas).

## État actuel : la navigation inter-écrans n'existe pas encore

Au moment d'écrire cette skill, `AriseApp` pointe directement sur `HomeScreen`
(`home: const HomeScreen()`) ; la barre de navigation posée en bas de l'Accueil est purement
visuelle (voir le commit qui l'introduit — « la navigation entre écrans reste à câbler »).
**Le premier test fonctionnel ne peut donc couvrir qu'un seul écran monté via la vraie app**,
pas un parcours multi-écrans. Ce n'est pas une limite à contourner en inventant un routeur
hors tâche — quand une tâche pose la navigation réelle (probablement une tâche `Infra` ou
`Frontend` dédiée), les tests fonctionnels suivants pourront traverser plusieurs écrans dans
le même `pumpAndSettle` (taper sur un item de la barre de nav, vérifier l'écran suivant).

## Ce que ce test ne remplace pas

Les trois états (chargement/donnée/erreur) de chaque écran, la validation de formulaire, le
détail d'un widget isolé : ça reste la responsabilité des widget tests par écran — un test
fonctionnel qui vérifierait chaque état intermédiaire serait lent et redondant avec ce qui est
déjà couvert. Un ou deux parcours fonctionnels (le chemin heureux du démarrage, plus tard un
parcours multi-écrans) suffisent à ce stade.

Voir [[flutter-riverpod]] pour les conventions de widget test et les tokens de design,
[[commit-vert]] pour la cadence de commit.
