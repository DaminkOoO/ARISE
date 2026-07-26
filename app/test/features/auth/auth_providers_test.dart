import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:arise/features/auth/auth_providers.dart';
import 'package:arise/features/auth/auth_service.dart';

void main() {
  test("par défaut, l'app parle à l'API réelle et non à un placeholder", () {
    final conteneur = ProviderContainer();
    addTearDown(conteneur.dispose);

    expect(conteneur.read(authServiceProvider), isA<AuthServiceHttp>());
  });
}
