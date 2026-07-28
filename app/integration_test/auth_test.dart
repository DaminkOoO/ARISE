import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:integration_test/integration_test.dart';

import 'package:arise/features/navigation/coquille_navigation.dart';
import 'package:arise/features/auth/auth_providers.dart';
import 'package:arise/features/auth/auth_screen.dart';
import 'package:arise/features/auth/auth_service.dart';
import 'package:arise/features/auth/token_store.dart';
import 'package:arise/l10n/textes.dart';
import 'package:arise/main.dart';

/// Stockage en mémoire : le Keystore Android n'est pas ce qu'on teste ici.
class _StockageEnMemoire implements TokenStore {
  String? jeton;

  @override
  Future<void> enregistrer(String token) async => jeton = token;

  @override
  Future<String?> lire() async => jeton;

  @override
  Future<void> effacer() async => jeton = null;
}

/// Backend simulé au niveau du transport : c'est le **vrai** AuthServiceHttp
/// qui tourne (URL, corps, décodage, traduction des erreurs), seul le socket
/// est doublé. Jamais d'appel réseau réel, même en test fonctionnel.
http.Response _json(int statut, Map<String, Object?> corps) =>
    http.Response.bytes(
      utf8.encode(jsonEncode(corps)),
      statut,
      headers: {'content-type': 'application/problem+json'},
    );

Future<void> _demarrer(
  WidgetTester tester, {
  required MockClient client,
  required TokenStore stockage,
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        tokenStoreProvider.overrideWithValue(stockage),
        authServiceProvider.overrideWithValue(
          AuthServiceHttp(client: client, urlBase: 'http://api.test:5006'),
        ),
      ],
      child: const AriseApp(),
    ),
  );
  await tester.pumpAndSettle();
}

Future<void> _remplirEtSoumettre(WidgetTester tester, String nom) async {
  await tester.enterText(find.byKey(const Key('champ-nom')), nom);
  await tester.enterText(
    find.byKey(const Key('champ-mot-de-passe')),
    'MotDePasseSolide123!',
  );
  await tester.tap(find.byKey(const Key('bouton-soumettre')));
  await tester.pumpAndSettle();
}

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets(
    "un Chasseur inconnu s'inscrit depuis l'écran d'entrée et atteint l'accueil",
    (tester) async {
      final appels = <String>[];
      final stockage = _StockageEnMemoire();

      await _demarrer(
        tester,
        stockage: stockage,
        client: MockClient((requete) async {
          appels.add(requete.url.path);
          if (requete.url.path == '/auth/register') {
            return _json(201, {'userId': 'b3a33595', 'username': 'KAEL'});
          }
          return _json(200, {
            'accessToken': 'jwt-du-parcours',
            'expiresAt': '2026-07-26T16:41:16+00:00',
          });
        }),
      );

      expect(find.byType(AuthScreen), findsOneWidget);

      await tester.tap(find.text(Textes.sInscrire));
      await tester.pump();
      await _remplirEtSoumettre(tester, 'KAEL');

      expect(appels, ['/auth/register', '/auth/login']);
      expect(stockage.jeton, 'jwt-du-parcours');
      expect(find.byType(CoquilleNavigation), findsOneWidget);
    },
  );

  testWidgets(
    "des identifiants refusés laissent le Chasseur sur l'écran d'entrée, "
    'avec un motif en français',
    (tester) async {
      await _demarrer(
        tester,
        stockage: _StockageEnMemoire(),
        client: MockClient(
          (_) async => _json(401, {
            'status': 401,
            'detail': 'Nom de Chasseur ou mot de passe incorrect.',
          }),
        ),
      );

      await _remplirEtSoumettre(tester, 'KAEL');

      expect(find.byType(CoquilleNavigation), findsNothing);
      expect(
        find.text('Nom de Chasseur ou mot de passe incorrect.'),
        findsOneWidget,
      );
    },
  );

  testWidgets(
    "le Système injoignable ne montre jamais d'exception brute au Chasseur",
    (tester) async {
      await _demarrer(
        tester,
        stockage: _StockageEnMemoire(),
        client: MockClient(
          (_) async => throw http.ClientException('Connection refused'),
        ),
      );

      await _remplirEtSoumettre(tester, 'KAEL');

      expect(find.text(Textes.serveurInjoignable), findsOneWidget);
      expect(find.byType(AuthScreen), findsOneWidget);
    },
  );
}
