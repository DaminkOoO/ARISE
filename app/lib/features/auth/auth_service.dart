import 'auth_models.dart';

/// Frontière backend de l'authentification. En production, elle appelle l'API
/// (`/auth/login` ou `/auth/register`) et renvoie le JWT émis. En test, un
/// double la remplace — jamais d'appel réseau réel dans la suite.
abstract interface class AuthService {
  Future<String> authentifier(AuthMode mode, String nom, String motDePasse);
}

/// Placeholder tant que l'API d'auth n'est pas branchée : refuse toujours, avec
/// une erreur explicite. À remplacer par l'implémentation HTTP réelle.
class AuthServiceNonBranche implements AuthService {
  const AuthServiceNonBranche();

  @override
  Future<String> authentifier(AuthMode mode, String nom, String motDePasse) {
    throw UnimplementedError(
      "L'API d'authentification n'est pas encore branchée.",
    );
  }
}
