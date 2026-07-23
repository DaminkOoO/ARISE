import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:arise/theme/arise_theme.dart';
import 'package:arise/widgets/hexagon.dart';

import '../support/fonts.dart';
import '../support/harness.dart';

void main() {
  setUpAll(initialiserBinding);

  testWidgets('affiche la lettre de rang au centre', (tester) async {
    await tester.pumpWidget(
      enrober(const HexBadge(lettre: 'E', taille: 120)),
    );

    expect(find.text('E'), findsOneWidget);
  });

  testWidgets('découpe en hexagone', (tester) async {
    await tester.pumpWidget(
      enrober(const HexBadge(lettre: 'D', taille: 48)),
    );

    final clips = tester.widgetList<ClipPath>(find.byType(ClipPath));
    expect(clips.any((c) => c.clipper is HexagonClipper), isTrue);
  });

  testWidgets('respecte la taille demandée', (tester) async {
    await tester.pumpWidget(
      enrober(const HexBadge(lettre: 'S', taille: 96, accent: AriseColors.systeme)),
    );

    final taille = tester.getSize(find.byType(HexBadge));
    expect(taille.width, 96);
    expect(taille.height, 96);
  });
}
