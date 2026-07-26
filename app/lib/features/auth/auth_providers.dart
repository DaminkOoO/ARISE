import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:http/http.dart' as http;

import 'auth_models.dart';
import 'auth_service.dart';
import 'token_store.dart';

/// Service d'authentification injectable (remplacé par un double en test).
final authServiceProvider = Provider<AuthService>((ref) {
  final client = http.Client();
  ref.onDispose(client.close);
  return AuthServiceHttp(client: client);
});

/// Stockage sécurisé du JWT (remplacé par un double en test).
final tokenStoreProvider = Provider<TokenStore>(
  (ref) => const SecureTokenStore(FlutterSecureStorage()),
);

/// Jeton déjà en stockage sécurisé, lu une fois au démarrage : c'est lui qui
/// décide de l'écran d'entrée. `null` = aucun Chasseur connecté sur ce
/// téléphone.
final jetonStockeProvider = FutureProvider<String?>(
  (ref) => ref.watch(tokenStoreProvider).lire(),
);

/// Pilote la soumission du formulaire. L'état porte le JWT obtenu (ou null tant
/// qu'aucune tentative n'a abouti), et déballe chargement / erreur.
class AuthController extends Notifier<AsyncValue<String?>> {
  @override
  AsyncValue<String?> build() => const AsyncData(null);

  Future<void> soumettre(AuthMode mode, String nom, String motDePasse) async {
    state = const AsyncLoading();
    try {
      final token = await ref
          .read(authServiceProvider)
          .authentifier(mode, nom, motDePasse);
      await ref.read(tokenStoreProvider).enregistrer(token);
      state = AsyncData(token);
    } catch (erreur, pile) {
      state = AsyncError(erreur, pile);
    }
  }
}

final authControllerProvider =
    NotifierProvider<AuthController, AsyncValue<String?>>(AuthController.new);
