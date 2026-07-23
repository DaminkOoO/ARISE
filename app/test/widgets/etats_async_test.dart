import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:arise/l10n/textes.dart';
import 'package:arise/widgets/etats_async.dart';

import '../support/fonts.dart';
import '../support/harness.dart';

void main() {
  setUpAll(initialiserBinding);

  testWidgets('EtatChargement affiche un indicateur et un libellé Système',
      (tester) async {
    await tester.pumpWidget(enrober(const EtatChargement()));

    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    expect(find.text(Textes.synchronisation.toUpperCase()), findsOneWidget);
  });

  testWidgets('EtatErreur affiche le message français et un bouton Réessayer',
      (tester) async {
    var reessaye = false;
    await tester.pumpWidget(
      enrober(EtatErreur(onReessayer: () => reessaye = true)),
    );

    expect(find.text(Textes.erreurSysteme), findsOneWidget);
    await tester.tap(find.text(Textes.reessayer));
    expect(reessaye, isTrue);
  });
}
