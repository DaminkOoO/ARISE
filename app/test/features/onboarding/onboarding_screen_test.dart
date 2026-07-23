import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:arise/features/onboarding/onboarding_models.dart';
import 'package:arise/features/onboarding/onboarding_providers.dart';
import 'package:arise/features/onboarding/onboarding_screen.dart';

import '../../support/fonts.dart';

Future<ProviderContainer> _pump(
  WidgetTester tester, {
  ValueChanged<Objectif>? onContinuer,
}) async {
  final container = ProviderContainer();
  addTearDown(container.dispose);
  await tester.pumpWidget(
    UncontrolledProviderScope(
      container: container,
      child: MaterialApp(
        home: OnboardingScreen(onContinuer: onContinuer ?? (_) {}),
      ),
    ),
  );
  return container;
}

void main() {
  setUpAll(initialiserBinding);

  testWidgets('pose la question et ses quatre objectifs', (tester) async {
    await _pump(tester);

    expect(find.text('Quel est ton objectif ?'), findsOneWidget);
    expect(find.textContaining('ajustera tes quêtes'), findsOneWidget);
    expect(find.text('Sport'), findsOneWidget);
    expect(find.text('Budget'), findsOneWidget);
    expect(find.text('Habitudes'), findsOneWidget);
    expect(find.text('Tout'), findsOneWidget);
  });

  testWidgets('sélectionne Sport par défaut', (tester) async {
    final container = await _pump(tester);

    expect(container.read(objectifSelectionneProvider), Objectif.sport);
  });

  testWidgets('taper un objectif le sélectionne', (tester) async {
    final container = await _pump(tester);

    await tester.tap(find.text('Budget'));
    await tester.pump();

    expect(container.read(objectifSelectionneProvider), Objectif.budget);
  });

  testWidgets('Continuer remonte l\'objectif choisi', (tester) async {
    Objectif? choisi;
    await _pump(tester, onContinuer: (o) => choisi = o);

    await tester.tap(find.text('Habitudes'));
    await tester.pump();
    await tester.tap(find.text('Continuer'));

    expect(choisi, Objectif.habitudes);
  });
}
