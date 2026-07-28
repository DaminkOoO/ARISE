import 'habitudes_models.dart';

/// Frontière backend de l'écran Habitudes & Tâches. En test, un double la
/// remplace ; jamais d'appel réseau réel dans la suite.
abstract interface class HabitudesService {
  Future<HabitudesData> chargerHabitudes();
}

/// Données de démonstration, tant que les endpoints Habitudes & Tâches ne sont
/// pas exposés par l'API. Les valeurs sont figées, jamais calculées — surtout
/// pas les séries, qui appartiennent au backend.
class DemoHabitudesService implements HabitudesService {
  const DemoHabitudesService();

  @override
  Future<HabitudesData> chargerHabitudes() async {
    return const HabitudesData(
      habitudes: [
        Habitude(
          nom: 'Boire deux litres d\'eau',
          rythme: RythmeHabitude.quotidienne,
          serie: 5,
          tenueAujourdHui: true,
        ),
        Habitude(
          nom: 'Lire vingt minutes',
          rythme: RythmeHabitude.quotidienne,
          serie: 12,
          tenueAujourdHui: false,
        ),
        Habitude(
          nom: 'Ranger ton espace de travail',
          rythme: RythmeHabitude.hebdomadaire,
          serie: 3,
          tenueAujourdHui: false,
        ),
        Habitude(
          nom: 'Méditer cinq minutes',
          rythme: RythmeHabitude.quotidienne,
          serie: 0,
          tenueAujourdHui: false,
        ),
      ],
      taches: [
        Tache(titre: 'Appeler le dentiste', echeance: 'Demain'),
        Tache(titre: 'Envoyer les documents', echeance: 'Vendredi'),
        Tache(titre: 'Ranger le garage'),
      ],
    );
  }
}
