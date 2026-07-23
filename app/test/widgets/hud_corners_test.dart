import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:arise/theme/arise_theme.dart';
import 'package:arise/widgets/hud_corners.dart';

import '../support/fonts.dart';
import '../support/harness.dart';

void main() {
  setUpAll(initialiserBinding);

  testWidgets('affiche l\'enfant qu\'on lui confie', (tester) async {
    await tester.pumpWidget(
      enrober(const HudCorners(child: Text('contenu'))),
    );

    expect(find.text('contenu'), findsOneWidget);
  });

  testWidgets('dessine quatre crochets de coin', (tester) async {
    await tester.pumpWidget(
      enrober(const HudCorners(accent: AriseColors.systeme, child: SizedBox())),
    );

    expect(find.byType(HudCornerBracket), findsNWidgets(4));
  });

  testWidgets('les crochets portent l\'accent fourni', (tester) async {
    await tester.pumpWidget(
      enrober(const HudCorners(accent: AriseColors.sport, child: SizedBox())),
    );

    final bracket = tester.widget<HudCornerBracket>(
      find.byType(HudCornerBracket).first,
    );
    expect(bracket.accent, AriseColors.sport);
  });
}
