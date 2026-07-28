import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:arise/features/navigation/coquille_navigation.dart';
import 'package:arise/features/auth/auth_models.dart';
import 'package:arise/features/auth/auth_providers.dart';
import 'package:arise/features/auth/auth_screen.dart';
import 'package:arise/features/auth/auth_service.dart';
import 'package:arise/features/auth/token_store.dart';
import 'package:arise/l10n/textes.dart';
import 'package:arise/main.dart';
import 'package:arise/widgets/etats_async.dart';

import 'support/fonts.dart';

/// Double du stockage sécurisé : le Keystore Android n'existe pas en test.
class _FakeTokenStore implements TokenStore {
  _FakeTokenStore({this.enregistre, this.echoueALaLecture = false});

  String? enregistre;
  final bool echoueALaLecture;

  /// Complété à la main pour tenir l'app en état de chargement.
  final lecture = Completer<String?>();
  bool lectureDifferee = false;

  @override
  Future<void> enregistrer(String token) async => enregistre = token;

  @override
  Future<String?> lire() {
    if (lectureDifferee) return lecture.future;
    if (echoueALaLecture) {
      return Future.error(StateError('Keystore indisponible'));
    }
    return Future.value(enregistre);
  }

  @override
  Future<void> effacer() async => enregistre = null;
}

class _FakeAuthService implements AuthService {
  const _FakeAuthService();

  @override
  Future<String> authentifier(AuthMode mode, String nom, String motDePasse) =>
      Future.value('jwt-neuf');
}

Future<void> _demarrer(WidgetTester tester, TokenStore store) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        tokenStoreProvider.overrideWithValue(store),
        authServiceProvider.overrideWithValue(const _FakeAuthService()),
      ],
      child: const AriseApp(),
    ),
  );
}

void main() {
  setUpAll(initialiserBinding);

  testWidgets("sans jeton stocké, l'app s'ouvre sur l'écran d'authentification",
      (tester) async {
    await _demarrer(tester, _FakeTokenStore());
    await tester.pumpAndSettle();

    expect(find.byType(AuthScreen), findsOneWidget);
    expect(find.byType(CoquilleNavigation), findsNothing);
  });

  testWidgets("avec un jeton stocké, l'app s'ouvre directement sur l'accueil",
      (tester) async {
    await _demarrer(tester, _FakeTokenStore(enregistre: 'jwt-deja-la'));
    await tester.pumpAndSettle();

    expect(find.byType(CoquilleNavigation), findsOneWidget);
    expect(find.byType(AuthScreen), findsNothing);
  });

  testWidgets('pendant la lecture du jeton, un état d\'attente — pas un écran '
      'blanc', (tester) async {
    final store = _FakeTokenStore()..lectureDifferee = true;
    await _demarrer(tester, store);
    await tester.pump();

    expect(find.byType(EtatChargement), findsOneWidget);
    expect(find.text(Textes.synchronisation.toUpperCase()), findsOneWidget);

    store.lecture.complete(null);
    await tester.pumpAndSettle();
    expect(find.byType(AuthScreen), findsOneWidget);
  });

  testWidgets("après une authentification réussie, le Chasseur atterrit sur "
      "l'accueil", (tester) async {
    final store = _FakeTokenStore();
    await _demarrer(tester, store);
    await tester.pumpAndSettle();

    await tester.enterText(find.byKey(const Key('champ-nom')), 'KAEL');
    await tester.enterText(
      find.byKey(const Key('champ-mot-de-passe')),
      'MotDePasseSolide123!',
    );
    await tester.tap(find.byKey(const Key('bouton-soumettre')));
    await tester.pumpAndSettle();

    expect(find.byType(CoquilleNavigation), findsOneWidget);
    expect(find.byType(AuthScreen), findsNothing);
    expect(store.enregistre, 'jwt-neuf');
  });

  testWidgets('un stockage illisible redemande l\'authentification plutôt que '
      'de casser', (tester) async {
    await _demarrer(tester, _FakeTokenStore(echoueALaLecture: true));
    await tester.pumpAndSettle();

    expect(find.byType(AuthScreen), findsOneWidget);
  });
}
