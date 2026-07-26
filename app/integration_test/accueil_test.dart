import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';

import 'package:arise/features/accueil/home_models.dart';
import 'package:arise/features/accueil/home_providers.dart';
import 'package:arise/features/accueil/home_service.dart';
import 'package:arise/features/auth/auth_providers.dart';
import 'package:arise/features/auth/token_store.dart';
import 'package:arise/main.dart';

/// Double de service injectable — jamais d'appel réseau réel, même en test
/// fonctionnel.
class _FakeHomeService implements HomeService {
  const _FakeHomeService();

  @override
  Future<HomeData> chargerAccueil() => Future.value(_demo);
}

/// Un Chasseur déjà connecté : l'app démarre désormais sur la porte d'entrée,
/// qui n'ouvre l'accueil que si un jeton est en stockage sécurisé.
class _JetonDejaLa implements TokenStore {
  const _JetonDejaLa();

  @override
  Future<void> enregistrer(String token) async {}

  @override
  Future<String?> lire() async => 'jwt-de-test';

  @override
  Future<void> effacer() async {}
}

const _demo = HomeData(
  chasseur: Chasseur(nom: 'KAEL MORGAN', niveau: 14, rang: 'D', serieJours: 12),
  rapport: RapportSysteme(
    message: "Chasseur, 3 quêtes t'attendent aujourd'hui.",
    horodatage: "06:12 — Aujourd'hui",
  ),
  stats: [
    Stat(libelle: 'FOR', valeur: 72, domaine: Domaine.sport),
    Stat(libelle: 'VIT', valeur: 65, domaine: Domaine.sport),
    Stat(libelle: 'INT', valeur: 58, domaine: Domaine.habitudes),
    Stat(libelle: 'OR', valeur: 80, domaine: Domaine.budget),
    Stat(libelle: 'PER', valeur: 45, domaine: Domaine.calendrier),
  ],
  quetes: [
    Quete(
      titre: 'Épreuve du Jour — 40 pompes',
      domaine: Domaine.sport,
      xp: 30,
      terminee: false,
    ),
  ],
);

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets(
    "démarre AriseApp et affiche l'accueil avec les données du Chasseur",
    (tester) async {
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            homeServiceProvider.overrideWithValue(const _FakeHomeService()),
            tokenStoreProvider.overrideWithValue(const _JetonDejaLa()),
          ],
          child: const AriseApp(),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('[RAPPORT DU SYSTÈME]'), findsOneWidget);
      expect(find.text('KAEL MORGAN'), findsOneWidget);
    },
  );
}
