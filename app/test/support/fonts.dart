import 'package:flutter_test/flutter_test.dart';

/// Initialise le binding pour la suite.
///
/// Les polices de la charte sont embarquées (déclarées dans pubspec), donc
/// aucun accès réseau n'a lieu pour les charger — ni en test ni en production.
void initialiserBinding() {
  TestWidgetsFlutterBinding.ensureInitialized();
}
