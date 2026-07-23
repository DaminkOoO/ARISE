import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:arise/theme/arise_theme.dart';
import 'package:arise/widgets/stat_bar.dart';

import '../support/fonts.dart';
import '../support/harness.dart';

void main() {
  setUpAll(initialiserBinding);

  testWidgets('affiche le libellé et la valeur de la stat', (tester) async {
    await tester.pumpWidget(
      enrober(const StatBar(libelle: 'FOR', valeur: 72, accent: AriseColors.sport)),
    );

    expect(find.text('FOR'), findsOneWidget);
    expect(find.text('72'), findsOneWidget);
  });

  testWidgets('remplit la barre proportionnellement à la valeur', (tester) async {
    await tester.pumpWidget(
      enrober(const StatBar(libelle: 'OR', valeur: 80, accent: AriseColors.budget)),
    );

    final remplissage = tester.widget<FractionallySizedBox>(
      find.byType(FractionallySizedBox),
    );
    expect(remplissage.widthFactor, closeTo(0.80, 0.001));
  });

  testWidgets('borne le remplissage entre 0 et 1', (tester) async {
    await tester.pumpWidget(
      enrober(const StatBar(libelle: 'PER', valeur: 140, accent: AriseColors.calendrier)),
    );

    final remplissage = tester.widget<FractionallySizedBox>(
      find.byType(FractionallySizedBox),
    );
    expect(remplissage.widthFactor, 1.0);
  });

  testWidgets('la portion remplie porte une lueur de l\'accent', (tester) async {
    await tester.pumpWidget(
      enrober(const StatBar(libelle: 'FOR', valeur: 50, accent: AriseColors.sport)),
    );

    final rempli = tester.widget<Container>(
      find.descendant(
        of: find.byType(FractionallySizedBox),
        matching: find.byType(Container),
      ),
    );
    final deco = rempli.decoration! as BoxDecoration;
    expect(deco.color, AriseColors.sport);
    expect(deco.boxShadow, isNotEmpty);
  });
}
