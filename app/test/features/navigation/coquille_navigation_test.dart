import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:arise/features/accueil/home_models.dart';
import 'package:arise/features/accueil/home_providers.dart';
import 'package:arise/features/accueil/home_screen.dart';
import 'package:arise/features/accueil/home_service.dart';
import 'package:arise/features/habitudes/habitudes_screen.dart';
import 'package:arise/features/navigation/coquille_navigation.dart';
import 'package:arise/features/sport/sport_screen.dart';

import '../../support/fonts.dart';

/// Services de démonstration : la coquille s'éprouve sur la navigation, pas sur
/// le chargement des données de chaque écran.
class _AccueilDeDemo implements HomeService {
  const _AccueilDeDemo();

  @override
  Future<HomeData> chargerAccueil() => const DemoHomeService().chargerAccueil();
}

Widget _app() {
  return ProviderScope(
    overrides: [homeServiceProvider.overrideWithValue(const _AccueilDeDemo())],
    child: const MaterialApp(home: CoquilleNavigation()),
  );
}

Future<void> _monter(WidgetTester tester) async {
  await tester.pumpWidget(_app());
  await tester.pumpAndSettle();
}

Future<void> _toucher(WidgetTester tester, String onglet) async {
  await tester.tap(find.text(onglet));
  await tester.pumpAndSettle();
}

void main() {
  setUpAll(initialiserBinding);

  testWidgets('ouvre sur l\'accueil', (tester) async {
    await _monter(tester);

    expect(find.byType(HomeScreen), findsOneWidget);
  });

  testWidgets('la barre porte les cinq domaines', (tester) async {
    await _monter(tester);

    expect(find.text('Accueil'), findsOneWidget);
    expect(find.text('Sport'), findsOneWidget);
    expect(find.text('Budget'), findsOneWidget);
    expect(find.text('Habitudes'), findsOneWidget);
    expect(find.text('Calendrier'), findsOneWidget);
  });

  // Le trou que cette coquille bouche : avant elle, aucun écran n'était
  // atteignable depuis un autre.
  testWidgets('ouvre l\'écran Sport depuis la barre', (tester) async {
    await _monter(tester);

    await _toucher(tester, 'Sport');

    expect(find.byType(SportScreen), findsOneWidget);
    expect(find.byType(HomeScreen), findsNothing);
  });

  testWidgets('ouvre l\'écran Habitudes depuis la barre', (tester) async {
    await _monter(tester);

    await _toucher(tester, 'Habitudes');

    expect(find.byType(HabitudesScreen), findsOneWidget);
  });

  testWidgets('revient à l\'accueil depuis un autre onglet', (tester) async {
    await _monter(tester);

    await _toucher(tester, 'Sport');
    await _toucher(tester, 'Accueil');

    expect(find.byType(HomeScreen), findsOneWidget);
  });

  // La barre ne disparaît jamais : c'est le seul chemin entre les domaines.
  testWidgets('garde la barre visible sur un autre onglet', (tester) async {
    await _monter(tester);

    await _toucher(tester, 'Habitudes');

    expect(find.text('Accueil'), findsOneWidget);
  });

  // Budget et Calendrier arrivent en phases 3 et 4. Le Système annonce ce qui
  // vient, il ne laisse pas un écran vide qu'on lirait comme une panne.
  for (final domaine in ['Budget', 'Calendrier']) {
    testWidgets('annonce que $domaine est à venir', (tester) async {
      await _monter(tester);

      await _toucher(tester, domaine);

      expect(find.textContaining('bientôt'), findsOneWidget);
    });
  }
}
