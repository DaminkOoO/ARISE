import 'package:flutter/painting.dart';

import '../../theme/arise_theme.dart';

/// Domaines de progression du Chasseur. La couleur d'accent de chaque domaine
/// vient de la charte (jeton unique), jamais codée en dur dans un écran.
enum Domaine {
  systeme,
  sport,
  budget,
  habitudes,
  calendrier;

  Color get couleur => switch (this) {
        Domaine.systeme => AriseColors.systeme,
        Domaine.sport => AriseColors.sport,
        Domaine.budget => AriseColors.budget,
        Domaine.habitudes => AriseColors.habitudes,
        Domaine.calendrier => AriseColors.calendrier,
      };
}

/// Résumé du Chasseur tel que renvoyé par l'API. L'app l'affiche, elle ne
/// recalcule ni le niveau, ni le rang, ni la série.
class Chasseur {
  const Chasseur({
    required this.nom,
    required this.niveau,
    required this.rang,
    required this.serieJours,
  });

  final String nom;
  final int niveau;
  final String rang;
  final int serieJours;
}

/// Une ligne de stat (score sur 100 fourni par le moteur central).
class Stat {
  const Stat({
    required this.libelle,
    required this.valeur,
    required this.domaine,
  });

  final String libelle;
  final int valeur;
  final Domaine domaine;
}

/// Message du Système accompagné de son horodatage.
class RapportSysteme {
  const RapportSysteme({required this.message, required this.horodatage});

  final String message;
  final String horodatage;
}

/// Une quête du jour.
class Quete {
  const Quete({
    required this.titre,
    required this.domaine,
    required this.xp,
    required this.terminee,
  });

  final String titre;
  final Domaine domaine;
  final int xp;
  final bool terminee;
}

/// Agrégat des données de l'écran Accueil.
class HomeData {
  const HomeData({
    required this.chasseur,
    required this.rapport,
    required this.stats,
    required this.quetes,
  });

  final Chasseur chasseur;
  final RapportSysteme rapport;
  final List<Stat> stats;
  final List<Quete> quetes;
}
