import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

/// Ces contrôles portent sur les manifestes Android, pas sur du Dart : sans
/// eux, l'app compile, la suite est verte, et **chaque** appel à l'API échoue
/// sur l'émulateur. Android bloque le HTTP en clair depuis l'API 28.
String _manifeste(String variante) =>
    File('android/app/src/$variante/AndroidManifest.xml').readAsStringSync();

void main() {
  test('le manifeste de debug autorise le HTTP en clair', () {
    expect(_manifeste('debug'), contains('android:usesCleartextTraffic="true"'));
  });

  test("le manifeste de debug accorde l'accès au réseau", () {
    expect(
      _manifeste('debug'),
      contains('android:name="android.permission.INTERNET"'),
    );
  });

  test("l'app livrée n'emporte pas l'autorisation de HTTP en clair", () {
    // Le HTTP en clair est un confort de développement local : il ne doit
    // suivre ni la variante principale, ni le profilage.
    expect(_manifeste('main'), isNot(contains('usesCleartextTraffic')));
    expect(_manifeste('profile'), isNot(contains('usesCleartextTraffic')));
  });

  test("l'app livrée accède quand même au réseau", () {
    // La permission INTERNET n'est pas un confort de développement : sans elle
    // dans le manifeste principal, l'APK de release ne joint aucune API.
    expect(
      _manifeste('main'),
      contains('android:name="android.permission.INTERNET"'),
    );
  });
}
