/// Rythme auquel le Chasseur s'est engagé à tenir une habitude. Il donne son
/// unité à la série : des jours pour une quotidienne, des semaines pour une
/// hebdomadaire.
enum RythmeHabitude { quotidienne, hebdomadaire }

/// Une habitude suivie par le Chasseur.
///
/// [serie] est **celle que le backend annonce** : l'app ne la recalcule jamais
/// depuis un journal local. Dupliquer la règle en Dart, c'est se garantir deux
/// vérités divergentes le jour où elle bouge.
class Habitude {
  const Habitude({
    required this.nom,
    required this.rythme,
    required this.serie,
    required this.tenueAujourdHui,
  });

  final String nom;
  final RythmeHabitude rythme;

  /// Longueur de la série, dans l'unité du [rythme]. Zéro n'est pas un échec :
  /// c'est une habitude qui reste à commencer.
  final int serie;

  /// Vraie quand l'habitude a déjà été validée pour la période en cours.
  final bool tenueAujourdHui;
}

/// Une tâche ponctuelle qu'il reste à faire.
class Tache {
  const Tache({required this.titre, this.echeance});

  final String titre;

  /// Échéance déjà mise en forme pour l'affichage, ou `null` quand le Chasseur
  /// ne s'en est donné aucune — auquel cas rien ne doit laisser croire qu'elle
  /// est en retard.
  final String? echeance;
}

/// Agrégat des données de l'écran Habitudes & Tâches.
class HabitudesData {
  const HabitudesData({required this.habitudes, required this.taches});

  final List<Habitude> habitudes;
  final List<Tache> taches;
}
