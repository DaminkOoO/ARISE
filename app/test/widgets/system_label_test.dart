import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:arise/theme/arise_theme.dart';
import 'package:arise/widgets/system_label.dart';

import '../support/fonts.dart';
import '../support/harness.dart';

void main() {
  setUpAll(initialiserBinding);

  testWidgets('affiche le libellé en majuscules', (tester) async {
    await tester.pumpWidget(enrober(const SystemLabel('Rapport du Système')));

    expect(find.text('RAPPORT DU SYSTÈME'), findsOneWidget);
  });

  testWidgets('utilise la police d\'étiquette JetBrains Mono', (tester) async {
    await tester.pumpWidget(enrober(const SystemLabel('Quêtes du jour')));

    final texte = tester.widget<Text>(find.byType(Text));
    expect(texte.style?.fontFamily, AriseTypography.familleEtiquette);
  });

  testWidgets('prend une couleur d\'accent quand on la fournit', (tester) async {
    await tester.pumpWidget(
      enrober(const SystemLabel('Système', accent: AriseColors.glow)),
    );

    final texte = tester.widget<Text>(find.byType(Text));
    expect(texte.style?.color, AriseColors.glow);
  });
}
