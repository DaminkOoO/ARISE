import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:arise/features/eveil/eveil_models.dart';
import 'package:arise/features/eveil/eveil_providers.dart';
import 'package:arise/features/eveil/eveil_screen.dart';
import 'package:arise/features/accueil/home_models.dart';
import 'package:arise/widgets/hexagon.dart';

import '../../support/fonts.dart';

const _eveil = EveilData(
  rang: 'E',
  stats: [
    Stat(libelle: 'FOR', valeur: 10, domaine: Domaine.sport),
    Stat(libelle: 'VIT', valeur: 10, domaine: Domaine.sport),
    Stat(libelle: 'INT', valeur: 10, domaine: Domaine.habitudes),
    Stat(libelle: 'OR', valeur: 10, domaine: Domaine.budget),
    Stat(libelle: 'PER', valeur: 10, domaine: Domaine.calendrier),
  ],
);

Widget _app({VoidCallback? onEntrer}) {
  return ProviderScope(
    overrides: [eveilDataProvider.overrideWithValue(_eveil)],
    child: MaterialApp(
      home: EveilScreen(onEntrer: onEntrer ?? () {}),
    ),
  );
}

void main() {
  setUpAll(initialiserBinding);

  testWidgets('annonce l\'éveil et le rang de départ', (tester) async {
    await tester.pumpWidget(_app());

    expect(find.text("L'ÉVEIL A COMMENCÉ"), findsOneWidget);
    expect(find.textContaining('Rang de départ : E'), findsOneWidget);
  });

  testWidgets('affiche le badge de rang hexagonal', (tester) async {
    await tester.pumpWidget(_app());

    expect(find.byType(HexBadge), findsOneWidget);
    expect(find.text('E'), findsOneWidget);
  });

  testWidgets('révèle les cinq stats de départ à 10', (tester) async {
    await tester.pumpWidget(_app());

    expect(find.text('10'), findsNWidgets(5));
    expect(find.text('FOR'), findsOneWidget);
    expect(find.text('PER'), findsOneWidget);
  });

  testWidgets('le bouton entre dans le Système', (tester) async {
    var entre = false;
    await tester.pumpWidget(_app(onEntrer: () => entre = true));

    await tester.tap(find.text('Entrer dans le Système'));
    expect(entre, isTrue);
  });
}
