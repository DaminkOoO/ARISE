import 'package:flutter/painting.dart';

import '../../theme/arise_theme.dart';

/// Objectif choisi par le Chasseur à l'onboarding. Le Système ajustera les
/// quêtes en conséquence. La couleur d'accent vient de la charte.
enum Objectif {
  sport('Sport', AriseColors.sport),
  budget('Budget', AriseColors.budget),
  habitudes('Habitudes', AriseColors.habitudes),
  tout('Tout', AriseColors.systeme);

  const Objectif(this.libelle, this.couleur);

  final String libelle;
  final Color couleur;
}
