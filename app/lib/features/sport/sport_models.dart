/// Quête sportive du jour, telle que générée par le Système côté backend.
/// L'app l'affiche comme un défi de jeu — jamais comme une consigne médicale.
class QueteSport {
  const QueteSport({
    required this.titre,
    required this.description,
    required this.xp,
  });

  final String titre;
  final String description;
  final int xp;
}

/// Une stat sportive (score sur 100 renvoyé par le moteur central).
class StatSport {
  const StatSport({required this.libelle, required this.valeur});

  final String libelle;
  final int valeur;
}

/// Une séance passée dans l'historique récent.
class SeanceSport {
  const SeanceSport({required this.titre, required this.quand});

  final String titre;
  final String quand;
}

/// Agrégat des données de l'écran Sport.
class SportData {
  const SportData({
    required this.quete,
    required this.stats,
    required this.historique,
  });

  final QueteSport quete;
  final List<StatSport> stats;
  final List<SeanceSport> historique;
}
