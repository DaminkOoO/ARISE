/// Libellés français de l'app, centralisés pour que la relecture du ton
/// (tutoiement, registre « Système », jamais culpabilisant) se fasse à un seul
/// endroit. On dit « Chasseur », jamais « utilisateur » ni « pseudo ».
abstract final class Textes {
  // États transverses des écrans de données.
  static const String synchronisation = 'Synchronisation avec le Système…';
  static const String erreurSysteme =
      "Le Système est momentanément hors de portée. Réessaie dans un instant.";
  static const String reessayer = 'Réessayer';

  // Étiquettes Système récurrentes.
  static const String chasseur = 'Chasseur';
  static const String rapportDuSysteme = '[Rapport du Système]';
  static const String quetesDuJour = 'Quêtes du jour';
  static const String niveauCourt = 'NIV';

  // Éveil / onboarding.
  static const String systeme = '[Système]';
  static const String eveilTitre = "L'ÉVEIL A COMMENCÉ";
  static const String entrerDansLeSysteme = 'Entrer dans le Système';
  static const String objectifQuestion = 'Quel est ton objectif ?';
  static const String objectifSousTitre =
      'Le Système ajustera tes quêtes en conséquence.';
  static const String continuer = 'Continuer';

  static String chasseurCree(String rang) =>
      'Chasseur créé. Rang de départ : $rang.';

  // Accueil.
  static const String serieActuelle = 'Série actuelle';

  static String serieEnJours(int jours) => '$jours jours';
  static String xp(int montant) => '+$montant XP';
}
