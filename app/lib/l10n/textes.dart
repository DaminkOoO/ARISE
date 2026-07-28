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

  // Authentification.
  static const String connexion = 'Connexion';
  static const String inscription = 'Inscription';
  static const String seConnecter = 'Se connecter';
  static const String seEveiller = "S'éveiller";
  static const String sInscrire = "S'inscrire";
  static const String jaiDejaUnCompte = "J'ai déjà un compte";
  static const String nomChasseur = 'Nom de Chasseur';
  static const String motDePasse = 'Mot de passe';
  static const String nomChasseurVide = 'Renseigne ton nom de Chasseur.';
  static const String motDePasseVide = 'Renseigne ton mot de passe.';
  static const String identifiantsRefuses =
      'Identifiants refusés. Vérifie ton nom de Chasseur et ton mot de passe.';
  static const String authAccroche = 'Le Système attend ton éveil.';
  static const String nomChasseurDejaPris =
      'Ce nom de Chasseur est déjà pris. Choisis-en un autre.';
  static const String serveurInjoignable =
      'Le Système est injoignable. Vérifie ta connexion, puis réessaie.';

  // Sport.
  static const String sportTitre = 'SPORT';
  static const String queteDuJour = '[Quête du Jour]';
  static const String terminer = 'Terminer';
  static const String historiqueRecent = 'Historique récent';

  // Habitudes & Tâches.
  static const String habitudesTitre = 'HABITUDES & TÂCHES';
  static const String sectionHabitudes = '[Habitudes]';
  static const String sectionTaches = '[Tâches]';

  /// Une série à zéro n'est pas un échec à afficher : « 0 jour » pointerait un
  /// manquement, là où le Système invite (règle n°5).
  static const String serieACommencer = 'À commencer';
  static const String sansEcheance = 'Sans échéance';
  static const String aucuneHabitude =
      "Aucune habitude pour l'instant. Le Système peut t'en proposer.";
  static const String aucuneTache = "Rien ne t'attend pour l'instant.";
  static const String ajouter = 'Ajouter';

  static String serieEnSemaines(int semaines) =>
      semaines <= 1 ? '$semaines semaine' : '$semaines semaines';

  // Accueil.
  static const String serieActuelle = 'Série actuelle';
  static const String navAccueil = 'Accueil';
  static const String navSport = 'Sport';
  static const String navBudget = 'Budget';
  static const String navHabitudes = 'Habitudes';
  static const String navCalendrier = 'Calendrier';

  /// Un domaine dont l'écran n'existe pas encore. Le Système annonce ce qui
  /// vient : un écran vide se lirait comme une panne.
  static const String domaineAVenir =
      "Le Système ouvre ce domaine bientôt. Reviens y jeter un œil.";

  static String serieEnJours(int jours) =>
      jours <= 1 ? '$jours jour' : '$jours jours';
  static String xp(int montant) => '+$montant XP';
}
