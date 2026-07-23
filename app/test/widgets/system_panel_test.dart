import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:arise/theme/arise_theme.dart';
import 'package:arise/widgets/hud_corners.dart';
import 'package:arise/widgets/system_panel.dart';

import '../support/fonts.dart';
import '../support/harness.dart';

BoxDecoration _decorationDe(WidgetTester tester) {
  final container = tester.widget<Container>(
    find.descendant(
      of: find.byType(SystemPanel),
      matching: find.byType(Container),
    ).first,
  );
  return container.decoration! as BoxDecoration;
}

void main() {
  setUpAll(initialiserBinding);

  testWidgets('affiche son contenu sur le fond des panneaux', (tester) async {
    await tester.pumpWidget(
      enrober(const SystemPanel(child: Text('rapport'))),
    );

    expect(find.text('rapport'), findsOneWidget);
    expect(_decorationDe(tester).color, AriseColors.panneau);
  });

  testWidgets('sans glow, aucune ombre lumineuse', (tester) async {
    await tester.pumpWidget(
      enrober(const SystemPanel(child: SizedBox())),
    );

    expect(_decorationDe(tester).boxShadow, anyOf(isNull, isEmpty));
  });

  testWidgets('avec glow, porte une ombre lumineuse de l\'accent', (tester) async {
    await tester.pumpWidget(
      enrober(const SystemPanel(
        accent: AriseColors.sport,
        glow: true,
        child: SizedBox(),
      )),
    );

    final ombres = _decorationDe(tester).boxShadow!;
    expect(ombres, isNotEmpty);
    expect(ombres.first.color.a, greaterThan(0));
    expect(ombres.first.blurRadius, greaterThan(10));
  });

  testWidgets('avec coinsHud, superpose les crochets HUD', (tester) async {
    await tester.pumpWidget(
      enrober(const SystemPanel(coinsHud: true, child: SizedBox())),
    );

    expect(find.byType(HudCorners), findsOneWidget);
  });
}
