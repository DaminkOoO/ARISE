import 'package:flutter/material.dart';

import '../theme/arise_theme.dart';

/// Découpe hexagonale de la charte : polygon(25% 0,75% 0,100% 50,75% 100,
/// 25% 100,0 50). Sert aux badges de rang.
class HexagonClipper extends CustomClipper<Path> {
  const HexagonClipper();

  @override
  Path getClip(Size size) {
    final w = size.width;
    final h = size.height;
    return Path()
      ..moveTo(w * 0.25, 0)
      ..lineTo(w * 0.75, 0)
      ..lineTo(w, h * 0.5)
      ..lineTo(w * 0.75, h)
      ..lineTo(w * 0.25, h)
      ..lineTo(0, h * 0.5)
      ..close();
  }

  @override
  bool shouldReclip(covariant HexagonClipper oldClipper) => false;
}

/// Badge de rang hexagonal : hexagone d'accent lumineux, hexagone de fond
/// intérieur, lettre de rang en Rajdhani. Élément héros de l'accueil, de la
/// fenêtre de statut et de l'écran d'Éveil — une seule définition.
class HexBadge extends StatelessWidget {
  const HexBadge({
    required this.lettre,
    required this.taille,
    this.accent = AriseColors.systeme,
    super.key,
  });

  /// Lettre de rang (E, D, C, …).
  final String lettre;

  /// Côté du badge (px).
  final double taille;

  /// Couleur d'accent du rang.
  final Color accent;

  @override
  Widget build(BuildContext context) {
    final bordure = taille * 0.06;
    return SizedBox(
      width: taille,
      height: taille,
      child: DecoratedBox(
        decoration: BoxDecoration(
          boxShadow: [
            BoxShadow(
              color: AriseColors.glow.withValues(alpha: 0.5),
              blurRadius: taille * 0.28,
            ),
          ],
        ),
        child: ClipPath(
          clipper: const HexagonClipper(),
          child: Container(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
                colors: [accent, AriseColors.glow],
              ),
            ),
            padding: EdgeInsets.all(bordure),
            child: ClipPath(
              clipper: const HexagonClipper(),
              child: ColoredBox(
                color: AriseColors.fond,
                child: Center(
                  child: Text(
                    lettre,
                    style: AriseTypography.titre.copyWith(
                      fontSize: taille * 0.42,
                      color: AriseColors.glow,
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
