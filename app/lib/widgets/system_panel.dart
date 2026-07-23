import 'package:flutter/material.dart';

import '../theme/arise_theme.dart';
import 'hud_corners.dart';

/// Panneau « fenêtre du Système » : fond sombre, bordure, lueur et coins HUD
/// optionnels. Brique de base des cartes de tous les écrans.
class SystemPanel extends StatelessWidget {
  const SystemPanel({
    required this.child,
    this.accent = AriseColors.systeme,
    this.glow = false,
    this.coinsHud = false,
    this.bordureAccent = false,
    this.padding = const EdgeInsets.all(AriseSpacing.panneau),
    super.key,
  });

  final Widget child;

  /// Couleur d'accent du panneau (bordure accentuée, lueur, coins).
  final Color accent;

  /// Ajoute une lueur (box-shadow) de l'accent autour du panneau.
  final bool glow;

  /// Superpose les quatre crochets HUD.
  final bool coinsHud;

  /// Utilise l'accent pour la bordure au lieu de la bordure neutre.
  final bool bordureAccent;

  final EdgeInsetsGeometry padding;

  @override
  Widget build(BuildContext context) {
    final panneau = Container(
      padding: padding,
      decoration: BoxDecoration(
        color: AriseColors.panneau,
        border: Border.all(
          color: bordureAccent || glow
              ? accent.withValues(alpha: 0.5)
              : AriseColors.bordure,
        ),
        boxShadow: glow
            ? [
                BoxShadow(
                  color: accent.withValues(alpha: 0.3),
                  blurRadius: 24,
                ),
              ]
            : null,
      ),
      child: child,
    );

    if (!coinsHud) return panneau;
    return HudCorners(accent: accent, debord: 2, child: panneau);
  }
}
