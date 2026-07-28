import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:arise/features/habitudes/habitudes_models.dart';
import 'package:arise/features/habitudes/habitudes_providers.dart';
import 'package:arise/features/habitudes/habitudes_screen.dart';
import 'package:arise/features/habitudes/habitudes_service.dart';
import 'package:arise/widgets/hud_corners.dart';

import '../../support/fonts.dart';

class _FakeHabitudesService implements HabitudesService {
  _FakeHabitudesService.donnee(this._data) : _erreur = null;
  _FakeHabitudesService.erreur(this._erreur) : _data = null;
  _FakeHabitudesService.jamais()
      : _data = null,
        _erreur = null,
        _bloque = true;

  final HabitudesData? _data;
  final Object? _erreur;
  bool _bloque = false;

  @override
  Future<HabitudesData> chargerHabitudes() {
    if (_bloque) return Completer<HabitudesData>().future;
    if (_erreur != null) return Future.error(_erreur);
    return Future.value(_data);
  }
}

const _demo = HabitudesData(
  habitudes: [
    Habitude(
      nom: 'Boire deux litres d\'eau',
      rythme: RythmeHabitude.quotidienne,
      serie: 5,
      tenueAujourdHui: true,
    ),
    Habitude(
      nom: 'Séance longue',
      rythme: RythmeHabitude.hebdomadaire,
      serie: 3,
      tenueAujourdHui: false,
    ),
  ],
  taches: [
    Tache(titre: 'Appeler le dentiste', echeance: 'Demain'),
    Tache(titre: 'Ranger le garage'),
  ],
);

Widget _app(HabitudesService service) {
  return ProviderScope(
    overrides: [habitudesServiceProvider.overrideWithValue(service)],
    child: const MaterialApp(home: HabitudesScreen()),
  );
}

void main() {
  setUpAll(initialiserBinding);

  testWidgets('état chargement : indicateur de synchronisation', (tester) async {
    await tester.pumpWidget(_app(_FakeHabitudesService.jamais()));
    await tester.pump();

    expect(find.byType(CircularProgressIndicator), findsOneWidget);
  });

  testWidgets('état erreur : message français non culpabilisant', (tester) async {
    await tester.pumpWidget(_app(_FakeHabitudesService.erreur(Exception('x'))));
    await tester.pumpAndSettle();

    expect(find.textContaining('Réessaie'), findsOneWidget);
  });

  group('état donnée', () {
    Future<void> monter(WidgetTester tester, [HabitudesData data = _demo]) async {
      await tester.pumpWidget(_app(_FakeHabitudesService.donnee(data)));
      await tester.pumpAndSettle();
    }

    testWidgets('affiche le titre et les deux sections', (tester) async {
      await monter(tester);

      expect(find.text('HABITUDES & TÂCHES'), findsOneWidget);
      expect(find.text('[HABITUDES]'), findsOneWidget);
      expect(find.text('[TÂCHES]'), findsOneWidget);
    });

    testWidgets('liste les habitudes du Chasseur', (tester) async {
      await monter(tester);

      expect(find.text('Boire deux litres d\'eau'), findsOneWidget);
      expect(find.text('Séance longue'), findsOneWidget);
    });

    // La série est celle que le backend annonce : l'app ne la recalcule jamais.
    testWidgets('affiche la série d\'une habitude quotidienne en jours',
        (tester) async {
      await monter(tester);

      expect(find.text('5 jours'), findsOneWidget);
    });

    // Le rythme donne son unité à la série — des semaines pour une hebdomadaire.
    testWidgets('affiche la série d\'une habitude hebdomadaire en semaines',
        (tester) async {
      await monter(tester);

      expect(find.text('3 semaines'), findsOneWidget);
    });

    testWidgets('accorde le singulier d\'une série d\'un seul jour',
        (tester) async {
      await monter(
        tester,
        const HabitudesData(
          habitudes: [
            Habitude(
              nom: 'Lire vingt minutes',
              rythme: RythmeHabitude.quotidienne,
              serie: 1,
              tenueAujourdHui: true,
            ),
          ],
          taches: [],
        ),
      );

      expect(find.text('1 jour'), findsOneWidget);
    });

    // Règle n°5 : une série à zéro n'est pas un échec à afficher. « 0 jour »
    // pointerait un manquement ; le Système invite, il ne reproche pas.
    testWidgets('une série à zéro invite au lieu de reprocher', (tester) async {
      await monter(
        tester,
        const HabitudesData(
          habitudes: [
            Habitude(
              nom: 'Méditer cinq minutes',
              rythme: RythmeHabitude.quotidienne,
              serie: 0,
              tenueAujourdHui: false,
            ),
          ],
          taches: [],
        ),
      );

      // Étiquette Système, donc en majuscules — comme « SANS ÉCHÉANCE », qui
      // occupe la même place sur la ligne d'une tâche.
      expect(find.text('À COMMENCER'), findsOneWidget);
      expect(find.textContaining('0 jour'), findsNothing);
    });

    testWidgets('liste les tâches à faire et leur échéance', (tester) async {
      await monter(tester);

      expect(find.text('Appeler le dentiste'), findsOneWidget);
      expect(find.text('DEMAIN'), findsOneWidget);
    });

    // Une tâche sans échéance n'est pas en retard : rien ne doit laisser croire
    // qu'elle l'est, pas même une date inventée.
    testWidgets('n\'invente pas d\'échéance pour une tâche qui n\'en a pas',
        (tester) async {
      await monter(tester);

      expect(find.text('Ranger le garage'), findsOneWidget);
      expect(find.text('SANS ÉCHÉANCE'), findsOneWidget);
    });

    testWidgets('les cartes portent des coins HUD', (tester) async {
      await monter(tester);

      expect(find.byType(HudCorners), findsWidgets);
    });

    testWidgets('offre un bouton d\'ajout', (tester) async {
      await monter(tester);

      expect(find.byType(FloatingActionButton), findsOneWidget);
      expect(find.byIcon(Icons.add), findsOneWidget);
    });

    // Listes vides : le Système propose, il ne constate pas un manque.
    testWidgets('sans habitude, invite plutôt que de laisser un vide',
        (tester) async {
      await monter(
        tester,
        const HabitudesData(habitudes: [], taches: []),
      );

      expect(find.textContaining('Le Système peut t\'en proposer'), findsOneWidget);
    });

    testWidgets('sans tâche, dit que rien n\'attend le Chasseur', (tester) async {
      await monter(
        tester,
        const HabitudesData(habitudes: [], taches: []),
      );

      expect(find.textContaining('Rien ne t\'attend'), findsOneWidget);
    });
  });
}
