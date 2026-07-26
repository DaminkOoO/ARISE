import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:arise/features/auth/auth_models.dart';
import 'package:arise/features/auth/auth_service.dart';
import 'package:arise/l10n/textes.dart';

/// Réponse JSON telle que l'API la rend réellement : des octets **UTF-8** sous
/// un content-type **sans charset** — vérifié au curl sur la pile qui tourne.
/// C'est le cas piégeux : `package:http` décode alors `.body` en latin-1, et
/// « déjà » arriverait mutilé à l'écran si le service lisait `.body`.
http.Response _reponseJson(int statut, Map<String, Object?> corps) =>
    http.Response.bytes(
      utf8.encode(jsonEncode(corps)),
      statut,
      headers: {'content-type': 'application/problem+json'},
    );

/// Corps d'erreur tel que l'API le rend : un ProblemDetails dont le `detail`
/// est déjà rédigé en français pour l'écran (voir AuthExceptionHandler).
http.Response _probleme(int statut, String detail) => _reponseJson(statut, {
  'type': 'https://tools.ietf.org/html/rfc9110',
  'title': 'Erreur',
  'status': statut,
  'detail': detail,
});

void main() {
  group('AuthServiceHttp — chemin nominal', () {
    test('la connexion appelle /auth/login et rend le jeton', () async {
      http.Request? vue;
      final service = AuthServiceHttp(
        urlBase: 'http://exemple.test:5006',
        client: MockClient((requete) async {
          vue = requete;
          return _reponseJson(200, {
            'accessToken': 'jwt-de-connexion',
            'expiresAt': '2026-07-26T16:41:16+00:00',
          });
        }),
      );

      final jeton = await service.authentifier(
        AuthMode.connexion,
        'KAEL',
        'MotDePasseSolide123!',
      );

      expect(jeton, 'jwt-de-connexion');
      expect(vue!.method, 'POST');
      expect(vue!.url.toString(), 'http://exemple.test:5006/auth/login');
      expect(jsonDecode(vue!.body), {
        'username': 'KAEL',
        'password': 'MotDePasseSolide123!',
      });
      expect(vue!.headers['content-type'], contains('application/json'));
    });

    test(
      "l'inscription enchaîne /auth/register puis /auth/login pour obtenir le jeton",
      () async {
        final chemins = <String>[];
        final service = AuthServiceHttp(
          urlBase: 'http://exemple.test:5006',
          client: MockClient((requete) async {
            chemins.add(requete.url.path);
            if (requete.url.path == '/auth/register') {
              return _reponseJson(201, {
                'userId': 'b3a33595',
                'username': 'KAEL',
              });
            }
            return _reponseJson(200, {
              'accessToken': 'jwt-neuf',
              'expiresAt': '2026-07-26T16:41:16+00:00',
            });
          }),
        );

        final jeton = await service.authentifier(
          AuthMode.inscription,
          'KAEL',
          'MotDePasseSolide123!',
        );

        expect(jeton, 'jwt-neuf');
        expect(chemins, ['/auth/register', '/auth/login']);
      },
    );

    test("l'URL de base par défaut vise la boucle locale de l'hôte "
        "vue depuis l'émulateur Android", () {
      expect(AuthServiceHttp.urlBaseParDefaut, 'http://10.0.2.2:5006');
    });
  });

  group('AuthServiceHttp — échecs, en français', () {
    Future<ErreurAuth> capturer(
      MockClient client, {
      AuthMode mode = AuthMode.connexion,
      Duration delai = const Duration(seconds: 10),
    }) async {
      final service = AuthServiceHttp(
        urlBase: 'http://exemple.test:5006',
        client: client,
        delai: delai,
      );
      try {
        await service.authentifier(mode, 'KAEL', 'MotDePasseSolide123!');
      } on ErreurAuth catch (erreur) {
        return erreur;
      }
      fail('Une ErreurAuth était attendue.');
    }

    test('409 à l\'inscription : le nom de Chasseur est déjà pris', () async {
      final erreur = await capturer(
        MockClient(
          (_) async => _probleme(
            409,
            'Ce nom de Chasseur est déjà pris. Choisis-en un autre.',
          ),
        ),
        mode: AuthMode.inscription,
      );

      expect(erreur.message, 'Ce nom de Chasseur est déjà pris. Choisis-en un autre.');
    });

    test('409 sans corps exploitable : repli sur le libellé du dépôt', () async {
      final erreur = await capturer(
        MockClient((_) async => http.Response('', 409)),
        mode: AuthMode.inscription,
      );

      expect(erreur.message, Textes.nomChasseurDejaPris);
    });

    test('401 : identifiants refusés, sans dire lequel des deux', () async {
      final erreur = await capturer(
        MockClient(
          (_) async => _probleme(401, 'Nom de Chasseur ou mot de passe incorrect.'),
        ),
      );

      expect(erreur.message, 'Nom de Chasseur ou mot de passe incorrect.');
    });

    test('401 sans corps : repli sur le libellé du dépôt', () async {
      final erreur = await capturer(
        MockClient((_) async => http.Response('', 401)),
      );

      expect(erreur.message, Textes.identifiantsRefuses);
    });

    test('400 : le message de validation du serveur remonte à l\'écran',
        () async {
      final erreur = await capturer(
        MockClient(
          (_) async => _probleme(
            400,
            'Le mot de passe doit contenir au moins 12 caractères.',
          ),
        ),
        mode: AuthMode.inscription,
      );

      expect(erreur.message, 'Le mot de passe doit contenir au moins 12 caractères.');
    });

    test('serveur injoignable : message français, jamais une exception brute',
        () async {
      final erreur = await capturer(
        MockClient(
          (_) async => throw const SocketException('Connection refused'),
        ),
      );

      expect(erreur.message, Textes.serveurInjoignable);
    });

    test('serveur trop lent : le même message, sans attendre indéfiniment',
        () async {
      final erreur = await capturer(
        MockClient((_) async {
          await Future<void>.delayed(const Duration(seconds: 30));
          return http.Response('', 200);
        }),
        delai: const Duration(milliseconds: 20),
      );

      expect(erreur.message, Textes.serveurInjoignable);
    });

    test('500 : jamais le détail du serveur, seulement le libellé Système',
        () async {
      final erreur = await capturer(
        MockClient((_) async => _probleme(500, 'An unhandled exception occurred.')),
      );

      expect(erreur.message, Textes.erreurSysteme);
    });

    test('200 au corps illisible : libellé Système plutôt qu\'un plantage',
        () async {
      final erreur = await capturer(
        MockClient((_) async => http.Response('pas du json', 200)),
      );

      expect(erreur.message, Textes.erreurSysteme);
    });
  });
}
