import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Range et relit le JWT du Chasseur. Le jeton ne vit jamais en clair : il est
/// confié au stockage sécurisé de la plateforme.
abstract interface class TokenStore {
  Future<void> enregistrer(String token);
  Future<String?> lire();
  Future<void> effacer();
}

/// Implémentation adossée à `flutter_secure_storage` (Keystore Android).
class SecureTokenStore implements TokenStore {
  const SecureTokenStore(this._storage);

  static const String _cle = 'arise_jwt';

  final FlutterSecureStorage _storage;

  @override
  Future<void> enregistrer(String token) =>
      _storage.write(key: _cle, value: token);

  @override
  Future<String?> lire() => _storage.read(key: _cle);

  @override
  Future<void> effacer() => _storage.delete(key: _cle);
}
