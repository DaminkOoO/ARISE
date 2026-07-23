import 'package:flutter/material.dart';

import '../theme/arise_theme.dart';

/// Un crochet de coin HUD en L (deux traits perpendiculaires).
///
/// C'est la brique de la signature « viseur de jeu vidéo » de la charte :
/// ~12–14px de côté, trait de 2px, couleur d'accent.
class HudCornerBracket extends StatelessWidget {
  const HudCornerBracket({
    required this.accent,
    required this.haut,
    required this.gauche,
    this.taille = AriseSpacing.coinHud,
    this.trait = AriseSpacing.coinHudTrait,
    super.key,
  });

  /// Couleur du crochet.
  final Color accent;

  /// Vrai pour un coin supérieur, faux pour un coin inférieur.
  final bool haut;

  /// Vrai pour un coin gauche, faux pour un coin droit.
  final bool gauche;

  final double taille;
  final double trait;

  @override
  Widget build(BuildContext context) {
    final cote = BorderSide(color: accent, width: trait);
    final vide = BorderSide.none;
    return SizedBox(
      width: taille,
      height: taille,
      child: DecoratedBox(
        decoration: BoxDecoration(
          border: Border(
            top: haut ? cote : vide,
            bottom: haut ? vide : cote,
            left: gauche ? cote : vide,
            right: gauche ? vide : cote,
          ),
        ),
      ),
    );
  }
}

/// Superpose quatre crochets de coin HUD aux angles de son enfant.
class HudCorners extends StatelessWidget {
  const HudCorners({
    required this.child,
    this.accent = AriseColors.systeme,
    this.debord = 0,
    super.key,
  });

  final Widget child;

  /// Couleur des crochets.
  final Color accent;

  /// Léger débord des crochets hors du cadre (effet viseur).
  final double debord;

  @override
  Widget build(BuildContext context) {
    return Stack(
      clipBehavior: Clip.none,
      children: [
        child,
        Positioned(
          top: -debord,
          left: -debord,
          child: HudCornerBracket(accent: accent, haut: true, gauche: true),
        ),
        Positioned(
          top: -debord,
          right: -debord,
          child: HudCornerBracket(accent: accent, haut: true, gauche: false),
        ),
        Positioned(
          bottom: -debord,
          left: -debord,
          child: HudCornerBracket(accent: accent, haut: false, gauche: true),
        ),
        Positioned(
          bottom: -debord,
          right: -debord,
          child: HudCornerBracket(accent: accent, haut: false, gauche: false),
        ),
      ],
    );
  }
}
