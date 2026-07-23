import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:arise/features/auth/auth_models.dart';
import 'package:arise/features/auth/auth_providers.dart';
import 'package:arise/features/auth/auth_screen.dart';
import 'package:arise/features/auth/auth_service.dart';
import 'package:arise/features/auth/token_store.dart';

import '../../support/fonts.dart';

class _FakeAuthService implements AuthService {
  _FakeAuthService({this.token = 'jwt-de-test', this.erreur});

  final String token;
  final Object? erreur;
  AuthMode? modeAppele;
  String? nomAppele;

  @override
  Future<String> authentifier(AuthMode mode, String nom, String motDePasse) {
    modeAppele = mode;
    nomAppele = nom;
    if (erreur != null) return Future.error(erreur!);
    return Future.value(token);
  }
}

class _FakeTokenStore implements TokenStore {
  String? enregistre;

  @override
  Future<void> enregistrer(String token) async => enregistre = token;

  @override
  Future<String?> lire() async => enregistre;

  @override
  Future<void> effacer() async => enregistre = null;
}

Future<void> _pump(
  WidgetTester tester, {
  required AuthService service,
  required TokenStore store,
  ValueChanged<String>? onAuthentifie,
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        authServiceProvider.overrideWithValue(service),
        tokenStoreProvider.overrideWithValue(store),
      ],
      child: MaterialApp(
        home: AuthScreen(onAuthentifie: onAuthentifie ?? (_) {}),
      ),
    ),
  );
}

void main() {
  setUpAll(initialiserBinding);

  testWidgets('propose la connexion et bascule vers l\'inscription',
      (tester) async {
    await _pump(tester, service: _FakeAuthService(), store: _FakeTokenStore());

    expect(find.text('Connexion'), findsWidgets);

    await tester.tap(find.text("S'inscrire"));
    await tester.pump();

    expect(find.text("S'éveiller"), findsWidgets);
  });

  testWidgets('refuse un formulaire vide sans appeler le service',
      (tester) async {
    final service = _FakeAuthService();
    await _pump(tester, service: service, store: _FakeTokenStore());

    await tester.tap(find.byKey(const Key('bouton-soumettre')));
    await tester.pump();

    expect(find.textContaining('nom de Chasseur'), findsWidgets);
    expect(service.nomAppele, isNull);
  });

  testWidgets('authentifie, stocke le jeton et remonte au parent',
      (tester) async {
    final service = _FakeAuthService(token: 'jwt-123');
    final store = _FakeTokenStore();
    String? recu;
    await _pump(
      tester,
      service: service,
      store: store,
      onAuthentifie: (t) => recu = t,
    );

    await tester.enterText(find.byKey(const Key('champ-nom')), 'KAEL');
    await tester.enterText(find.byKey(const Key('champ-mot-de-passe')), 'secret');
    await tester.tap(find.byKey(const Key('bouton-soumettre')));
    await tester.pumpAndSettle();

    expect(service.modeAppele, AuthMode.connexion);
    expect(store.enregistre, 'jwt-123');
    expect(recu, 'jwt-123');
  });

  testWidgets('affiche un message français si l\'authentification échoue',
      (tester) async {
    final service = _FakeAuthService(erreur: Exception('401'));
    await _pump(tester, service: service, store: _FakeTokenStore());

    await tester.enterText(find.byKey(const Key('champ-nom')), 'KAEL');
    await tester.enterText(find.byKey(const Key('champ-mot-de-passe')), 'x');
    await tester.tap(find.byKey(const Key('bouton-soumettre')));
    await tester.pumpAndSettle();

    expect(find.textContaining('Identifiants'), findsOneWidget);
  });
}
