import 'package:flutter/material.dart';

import '../theme/arise_theme.dart';

/// Étiquette Système : JetBrains Mono, MAJUSCULES, espacée et atténuée.
///
/// Rend les métadonnées de la charte comme `[RAPPORT DU SYSTÈME]` ou
/// `QUÊTES DU JOUR`. Le texte est mis en majuscules à l'affichage.
class SystemLabel extends StatelessWidget {
  const SystemLabel(this.texte, {this.accent, super.key});

  /// Libellé affiché ; il est passé en majuscules.
  final String texte;

  /// Couleur d'accent optionnelle (par défaut : texte atténué de la charte).
  final Color? accent;

  @override
  Widget build(BuildContext context) {
    return Text(
      texte.toUpperCase(),
      style: AriseTypography.etiquette.copyWith(color: accent),
    );
  }
}
