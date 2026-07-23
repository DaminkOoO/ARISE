import 'sport_models.dart';

/// Frontière backend de l'écran Sport. En test, un double la remplace ; jamais
/// d'appel réseau réel dans la suite.
abstract interface class SportService {
  Future<SportData> chargerSport();
}

/// Données de démonstration calquées sur la référence pixel, tant que le
/// backend Sport n'est pas branché. Les valeurs sont figées, jamais calculées.
class DemoSportService implements SportService {
  const DemoSportService();

  @override
  Future<SportData> chargerSport() async {
    return const SportData(
      quete: QueteSport(
        titre: "L'Épreuve du Guerrier",
        description: '40 pompes, 3 séries de squats, 5 minutes de gainage.',
        xp: 30,
      ),
      stats: [
        StatSport(libelle: 'Force', valeur: 72),
        StatSport(libelle: 'Vitesse', valeur: 65),
      ],
      historique: [
        SeanceSport(titre: 'Course — 5 km', quand: 'Hier'),
        SeanceSport(titre: 'Séance de musculation', quand: 'Il y a 2j'),
        SeanceSport(titre: 'Étirements — 15 min', quand: 'Il y a 3j'),
      ],
    );
  }
}
