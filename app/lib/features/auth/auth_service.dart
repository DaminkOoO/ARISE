import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:http/http.dart' as http;

import '../../l10n/textes.dart';
import 'auth_models.dart';

/// Frontière backend de l'authentification. En production, elle appelle l'API
/// (`/auth/login` ou `/auth/register`) et renvoie le JWT émis. En test, un
/// double la remplace — jamais d'appel réseau réel dans la suite.
abstract interface class AuthService {
  Future<String> authentifier(AuthMode mode, String nom, String motDePasse);
}

/// Échec d'authentification, déjà traduit pour l'écran.
///
/// Le widget affiche [message] tel quel : c'est ici, et nulle part dans l'UI,
/// que se décide ce que le Chasseur lit. Le message vient soit du serveur
/// (l'API rédige ses `ProblemDetails` en français, voir `AuthExceptionHandler`),
/// soit des libellés du dépôt quand le serveur ne dit rien d'exploitable.
class ErreurAuth implements Exception {
  const ErreurAuth(this.message);

  final String message;

  @override
  String toString() => 'ErreurAuth: $message';
}

/// Implémentation HTTP réelle, adossée à l'API ARISE.
///
/// L'inscription enchaîne **deux** appels : `/auth/register` ne rend que
/// l'identité du compte créé (`userId`, `username`), jamais de jeton — c'est
/// `/auth/login` qui l'émet. Le Chasseur, lui, ne voit qu'une seule action.
class AuthServiceHttp implements AuthService {
  AuthServiceHttp({
    http.Client? client,
    String? urlBase,
    Duration? delai,
  })  : _client = client ?? http.Client(),
        _urlBase = urlBase ?? urlBaseParDefaut,
        _delai = delai ?? const Duration(seconds: 10);

  /// URL de base de l'API, surchargeable au build :
  /// `flutter run --dart-define=ARISE_URL_API=https://api.exemple.fr`.
  ///
  /// La valeur par défaut est celle de l'émulateur Android : `localhost` y
  /// désigne l'émulateur lui-même, la boucle locale de la machine hôte s'y
  /// atteint par `10.0.2.2`. Le jour où l'API a un vrai domaine, c'est un
  /// `--dart-define` qui change, pas ce fichier.
  static const String urlBaseParDefaut = String.fromEnvironment(
    'ARISE_URL_API',
    defaultValue: 'http://10.0.2.2:5006',
  );

  final http.Client _client;
  final String _urlBase;
  final Duration _delai;

  @override
  Future<String> authentifier(
    AuthMode mode,
    String nom,
    String motDePasse,
  ) async {
    if (mode == AuthMode.inscription) {
      await _poster('/auth/register', nom, motDePasse);
    }
    final reponse = await _poster('/auth/login', nom, motDePasse);
    return _jetonDe(reponse);
  }

  Future<http.Response> _poster(
    String chemin,
    String nom,
    String motDePasse,
  ) async {
    final http.Response reponse;
    try {
      reponse = await _client
          .post(
            Uri.parse('$_urlBase$chemin'),
            headers: const {'content-type': 'application/json; charset=utf-8'},
            body: jsonEncode({'username': nom, 'password': motDePasse}),
          )
          .timeout(_delai);
    } on SocketException {
      throw const ErreurAuth(Textes.serveurInjoignable);
    } on http.ClientException {
      throw const ErreurAuth(Textes.serveurInjoignable);
    } on TimeoutException {
      throw const ErreurAuth(Textes.serveurInjoignable);
    }

    if (reponse.statusCode >= 200 && reponse.statusCode < 300) {
      return reponse;
    }
    throw _erreurDe(reponse);
  }

  /// Traduit le code HTTP en message affichable.
  ///
  /// Pour 400, 401 et 409, le `detail` du serveur est repris : c'est
  /// `AuthExceptionHandler` qui l'écrit, en français et pour l'écran — le
  /// dupliquer ici garantirait deux copies divergentes. Au-delà (5xx, réponse
  /// sans corps, corps illisible), on ne fait plus confiance au serveur : le
  /// 500 générique d'ASP.NET n'est pas rédigé pour le Chasseur, et peut être
  /// en anglais. On retombe alors sur les libellés du dépôt.
  ErreurAuth _erreurDe(http.Response reponse) {
    final detail = _detailDe(reponse);
    return switch (reponse.statusCode) {
      400 => ErreurAuth(detail ?? Textes.erreurSysteme),
      401 => ErreurAuth(detail ?? Textes.identifiantsRefuses),
      409 => ErreurAuth(detail ?? Textes.nomChasseurDejaPris),
      _ => const ErreurAuth(Textes.erreurSysteme),
    };
  }

  String? _detailDe(http.Response reponse) => _champTexte(reponse, 'detail');

  String _jetonDe(http.Response reponse) {
    final jeton = _champTexte(reponse, 'accessToken');
    if (jeton == null) throw const ErreurAuth(Textes.erreurSysteme);
    return jeton;
  }

  /// Lit un champ texte du corps JSON, ou `null` si le corps n'est pas
  /// exploitable.
  ///
  /// Les octets sont décodés en **UTF-8** sans se fier au content-type : l'API
  /// annonce `application/problem+json` **sans charset** (vérifié au curl), et
  /// `package:http` retombe alors sur latin-1 pour `.body` — « déjà pris »
  /// arriverait mutilé à l'écran. Le JSON est en UTF-8 par la RFC 8259.
  String? _champTexte(http.Response reponse, String champ) {
    try {
      final corps = jsonDecode(
        utf8.decode(reponse.bodyBytes, allowMalformed: true),
      );
      if (corps is Map<String, dynamic>) {
        final valeur = corps[champ];
        if (valeur is String && valeur.trim().isNotEmpty) return valeur;
      }
    } on FormatException {
      return null;
    }
    return null;
  }
}
