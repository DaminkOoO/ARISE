import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:arise/features/sport/sport_models.dart';
import 'package:arise/features/sport/sport_providers.dart';
import 'package:arise/features/sport/sport_screen.dart';
import 'package:arise/features/sport/sport_service.dart';
import 'package:arise/widgets/hud_corners.dart';

import '../../support/fonts.dart';

class _FakeSportService implements SportService {
  _FakeSportService.donnee(this._data) : _erreur = null;
  _FakeSportService.erreur(this._erreur) : _data = null;
  _FakeSportService.jamais()
      : _data = null,
        _erreur = null,
        _bloque = true;

  final SportData? _data;
  final Object? _erreur;
  bool _bloque = false;

  @override
  Future<SportData> chargerSport() {
    if (_bloque) return Completer<SportData>().future;
    if (_erreur != null) return Future.error(_erreur);
    return Future.value(_data);
  }
}

const _demo = SportData(
  quete: QueteSport(
    titre: 'L\'Épreuve du Guerrier',
    description: '40 pompes, 3 séries de squats, 5 minutes de gainage.',
    xp: 30,
  ),
  stats: [
    StatSport(libelle: 'Force', valeur: 72),
    StatSport(libelle: 'Vitesse', valeur: 65),
  ],
  historique: [
    SeanceSport(titre: 'Course — 5 km', quand: 'Hier'),
    SeanceSport(titre: 'Séance de musculation', quand: 'Il y a 2j'),
  ],
);

Widget _app(SportService service) {
  return ProviderScope(
    overrides: [sportServiceProvider.overrideWithValue(service)],
    child: const MaterialApp(home: SportScreen()),
  );
}

void main() {
  setUpAll(initialiserBinding);

  testWidgets('état chargement : indicateur de synchronisation', (tester) async {
    await tester.pumpWidget(_app(_FakeSportService.jamais()));
    await tester.pump();

    expect(find.byType(CircularProgressIndicator), findsOneWidget);
  });

  testWidgets('état erreur : message français non culpabilisant', (tester) async {
    await tester.pumpWidget(_app(_FakeSportService.erreur(Exception('x'))));
    await tester.pumpAndSettle();

    expect(find.textContaining('Réessaie'), findsOneWidget);
  });

  group('état donnée', () {
    testWidgets('affiche la quête du jour et son XP', (tester) async {
      await tester.pumpWidget(_app(_FakeSportService.donnee(_demo)));
      await tester.pumpAndSettle();

      expect(find.text('SPORT'), findsOneWidget);
      expect(find.text('[QUÊTE DU JOUR]'), findsOneWidget);
      expect(find.text('L\'Épreuve du Guerrier'), findsOneWidget);
      expect(find.textContaining('40 pompes'), findsOneWidget);
      expect(find.text('+30 XP'), findsOneWidget);
      expect(find.text('Terminer'), findsOneWidget);
    });

    testWidgets('la carte de quête porte des coins HUD', (tester) async {
      await tester.pumpWidget(_app(_FakeSportService.donnee(_demo)));
      await tester.pumpAndSettle();

      expect(find.byType(HudCorners), findsWidgets);
    });

    testWidgets('affiche les stats Force et Vitesse', (tester) async {
      await tester.pumpWidget(_app(_FakeSportService.donnee(_demo)));
      await tester.pumpAndSettle();

      // Les libellés de stat sont des métadonnées Système : MAJUSCULES.
      expect(find.text('FORCE'), findsOneWidget);
      expect(find.text('72'), findsOneWidget);
      expect(find.text('VITESSE'), findsOneWidget);
      expect(find.text('65'), findsOneWidget);
    });

    testWidgets('liste l\'historique récent', (tester) async {
      await tester.pumpWidget(_app(_FakeSportService.donnee(_demo)));
      await tester.pumpAndSettle();

      expect(find.text('Course — 5 km'), findsOneWidget);
      // L'horodatage relatif est une métadonnée Système : MAJUSCULES.
      expect(find.text('HIER'), findsOneWidget);
    });
  });
}
