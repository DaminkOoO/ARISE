import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:arise/theme/arise_theme.dart';
import 'package:arise/widgets/hud_background.dart';

import '../support/fonts.dart';
import '../support/harness.dart';

void main() {
  setUpAll(initialiserBinding);

  testWidgets('affiche son contenu', (tester) async {
    await tester.pumpWidget(
      enrober(const HudBackground(child: Text('écran'))),
    );

    expect(find.text('écran'), findsOneWidget);
  });

  testWidgets('pose un dégradé radial teinté par l\'accent', (tester) async {
    await tester.pumpWidget(
      enrober(const HudBackground(accent: AriseColors.sport, child: SizedBox())),
    );

    final container = tester.widget<Container>(
      find.descendant(
        of: find.byType(HudBackground),
        matching: find.byType(Container),
      ).first,
    );
    final deco = container.decoration! as BoxDecoration;
    expect(deco.color, AriseColors.fond);
    expect(deco.gradient, isA<RadialGradient>());
  });

  testWidgets('superpose une grille HUD', (tester) async {
    await tester.pumpWidget(
      enrober(const HudBackground(child: SizedBox())),
    );

    expect(find.byType(CustomPaint), findsWidgets);
  });
}
