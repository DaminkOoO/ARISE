import 'home_models.dart';

/// Frontière avec le backend : l'écran Accueil lit ses données ici. En
/// production, une implémentation appellera l'API ; en test, un double la
/// remplace. Jamais d'appel réseau réel dans la suite.
abstract interface class HomeService {
  Future<HomeData> chargerAccueil();
}

/// Données de démonstration, calquées sur la référence pixel, tant que le
/// moteur central backend n'est pas branché. Aucune formule de progression
/// n'est calculée ici : ce sont des valeurs figées.
class DemoHomeService implements HomeService {
  const DemoHomeService();

  @override
  Future<HomeData> chargerAccueil() async {
    return const HomeData(
      chasseur: Chasseur(
        nom: 'KAEL MORGAN',
        niveau: 14,
        rang: 'D',
        serieJours: 12,
      ),
      rapport: RapportSysteme(
        message:
            "Chasseur, 3 quêtes t'attendent aujourd'hui. Complète l'Épreuve "
            'du Jour pour progresser en FOR.',
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
        Quete(
          titre: 'Consigner les dépenses du jour',
          domaine: Domaine.budget,
          xp: 10,
          terminee: false,
        ),
        Quete(
          titre: 'Lecture — 20 minutes',
          domaine: Domaine.habitudes,
          xp: 15,
          terminee: true,
        ),
      ],
    );
  }
}
