import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:arise/features/accueil/home_models.dart';
import 'package:arise/features/accueil/home_providers.dart';
import 'package:arise/features/accueil/home_screen.dart';
import 'package:arise/features/accueil/home_service.dart';
import 'package:arise/widgets/hud_corners.dart';
import 'package:arise/widgets/stat_bar.dart';

import '../../support/fonts.dart';

/// Double de service injectable — jamais d'appel réseau réel.
class _FakeHomeService implements HomeService {
  _FakeHomeService.donnee(this._data) : _erreur = null;
  _FakeHomeService.erreur(this._erreur) : _data = null;
  _FakeHomeService.jamais()
      : _data = null,
        _erreur = null,
        _bloque = true;

  final HomeData? _data;
  final Object? _erreur;
  bool _bloque = false;

  @override
  Future<HomeData> chargerAccueil() {
    if (_bloque) return Completer<HomeData>().future;
    if (_erreur != null) return Future.error(_erreur);
    return Future.value(_data);
  }
}

const _demo = HomeData(
  chasseur: Chasseur(nom: 'KAEL MORGAN', niveau: 14, rang: 'D', serieJours: 12),
  rapport: RapportSysteme(
    message: "Chasseur, 3 quêtes t'attendent aujourd'hui.",
    horodatage: "06:12 — Aujourd'hui",
  ),
  stats: [
    Stat(libelle: 'FOR', valeur: 72, domaine: Domaine.sport),
    Stat(libelle: 'VIT', valeur: 65, domaine: Domaine.sport),
    Stat(libelle: 'INT', valeur: 58, domaine: Domaine.habitudes),
    Stat(libelle: 'OR', valeur: 80, domaine: Domaine.budget),
    Stat(libelle: 'PER', valeur: 45, domaine: Domaine.calendrier),
  ],
  quetes: [
    Quete(
      titre: 'Épreuve du Jour — 40 pompes',
      domaine: Domaine.sport,
      xp: 30,
      terminee: false,
    ),
  ],
);

Widget _app(HomeService service) {
  return ProviderScope(
    overrides: [homeServiceProvider.overrideWithValue(service)],
    child: const MaterialApp(home: HomeScreen()),
  );
}

void main() {
  setUpAll(initialiserBinding);

  testWidgets('état chargement : affiche un indicateur de synchronisation',
      (tester) async {
    await tester.pumpWidget(_app(_FakeHomeService.jamais()));
    await tester.pump();

    expect(find.byType(CircularProgressIndicator), findsOneWidget);
  });

  testWidgets('état erreur : affiche un message français non culpabilisant',
      (tester) async {
    await tester.pumpWidget(_app(_FakeHomeService.erreur(Exception('boom'))));
    await tester.pumpAndSettle();

    expect(find.textContaining('Système'), findsWidgets);
    expect(find.textContaining('Réessaie'), findsOneWidget);
  });

  group('état donnée', () {
    testWidgets('rend le nom, le niveau et le rang du Chasseur',
        (tester) async {
      await tester.pumpWidget(_app(_FakeHomeService.donnee(_demo)));
      await tester.pumpAndSettle();

      expect(find.text('KAEL MORGAN'), findsOneWidget);
      expect(find.text('14'), findsOneWidget);
      expect(find.text('D'), findsOneWidget);
    });

    testWidgets('affiche le rapport du Système et sa série', (tester) async {
      await tester.pumpWidget(_app(_FakeHomeService.donnee(_demo)));
      await tester.pumpAndSettle();

      expect(find.text('[RAPPORT DU SYSTÈME]'), findsOneWidget);
      expect(find.textContaining("quêtes t'attendent"), findsOneWidget);
      expect(find.textContaining('12'), findsWidgets);
    });

    testWidgets('rend les cinq barres de stats', (tester) async {
      await tester.pumpWidget(_app(_FakeHomeService.donnee(_demo)));
      await tester.pumpAndSettle();

      expect(find.byType(StatBar), findsNWidgets(5));
    });

    testWidgets('la carte rapport porte des coins HUD', (tester) async {
      await tester.pumpWidget(_app(_FakeHomeService.donnee(_demo)));
      await tester.pumpAndSettle();

      expect(find.byType(HudCorners), findsWidgets);
    });

    testWidgets('liste les quêtes du jour', (tester) async {
      await tester.pumpWidget(_app(_FakeHomeService.donnee(_demo)));
      await tester.pumpAndSettle();

      await tester.scrollUntilVisible(
        find.textContaining('Épreuve du Jour'),
        120,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.textContaining('Épreuve du Jour'), findsOneWidget);
    });

    // La barre de navigation n'est plus rendue par cet écran : elle appartient à
    // CoquilleNavigation, seule à pouvoir remplacer l'écran affiché. Sa
    // couverture vit désormais dans coquille_navigation_test.dart.
  });
}
