import 'package:flutter/material.dart';

import '../theme/arise_theme.dart';
import 'system_label.dart';

/// Barre de stat de l'accueil : étiquette, piste sombre, portion remplie avec
/// lueur, et valeur en Rajdhani. La valeur est un score sur 100 renvoyé par
/// l'API — l'app ne le recalcule pas.
class StatBar extends StatelessWidget {
  const StatBar({
    required this.libelle,
    required this.valeur,
    required this.accent,
    super.key,
  });

  /// Libellé court (FOR, VIT, INT, OR, PER).
  final String libelle;

  /// Valeur affichée, attendue entre 0 et 100.
  final int valeur;

  /// Couleur de domaine de la stat.
  final Color accent;

  @override
  Widget build(BuildContext context) {
    final fraction = (valeur / 100).clamp(0.0, 1.0);
    return Row(
      children: [
        SizedBox(width: 28, child: SystemLabel(libelle)),
        const SizedBox(width: 10),
        Expanded(
          child: SizedBox(
            height: 7,
            child: ColoredBox(
              color: AriseColors.bordure,
              child: Align(
                alignment: Alignment.centerLeft,
                child: FractionallySizedBox(
                  widthFactor: fraction,
                  child: Container(
                    decoration: BoxDecoration(
                      color: accent,
                      boxShadow: [
                        BoxShadow(
                          color: accent.withValues(alpha: 0.45),
                          blurRadius: 14,
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
        const SizedBox(width: 10),
        SizedBox(
          width: 26,
          child: Text(
            '$valeur',
            textAlign: TextAlign.right,
            style: AriseTypography.chiffreStat,
          ),
        ),
      ],
    );
  }
}
