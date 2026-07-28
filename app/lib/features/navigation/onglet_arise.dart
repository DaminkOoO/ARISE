import 'package:flutter/material.dart';

import '../../l10n/textes.dart';
import '../../theme/arise_theme.dart';

/// Les cinq domaines du Système, dans l'ordre de la barre de navigation.
///
/// L'accent de chaque onglet vient des jetons de la charte, jamais d'un hex
/// posé dans la barre : c'est le même vert qui teinte l'onglet Habitudes et les
/// séries de son écran.
enum OngletArise {
  accueil(Textes.navAccueil, AriseColors.systeme),
  sport(Textes.navSport, AriseColors.sport),
  budget(Textes.navBudget, AriseColors.budget),
  habitudes(Textes.navHabitudes, AriseColors.habitudes),
  calendrier(Textes.navCalendrier, AriseColors.calendrier);

  const OngletArise(this.libelle, this.accent);

  final String libelle;
  final Color accent;
}
