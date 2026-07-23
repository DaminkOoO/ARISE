import 'package:flutter/material.dart';

import '../theme/arise_theme.dart';

/// Texture de fond « fenêtre du Système » : dégradé radial subtil teinté par
/// l'accent de l'écran, superposé à une grille fine de 24px. Discret, jamais
/// distrayant.
class HudBackground extends StatelessWidget {
  const HudBackground({
    required this.child,
    this.accent = AriseColors.systeme,
    super.key,
  });

  final Widget child;

  /// Couleur du halo radial en haut de l'écran (accent de l'écran).
  final Color accent;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: AriseColors.fond,
        gradient: RadialGradient(
          center: const Alignment(0, -1),
          radius: 1.1,
          colors: [accent.withValues(alpha: 0.14), Colors.transparent],
          stops: const [0, 0.55],
        ),
      ),
      child: CustomPaint(
        painter: _GrilleHudPainter(),
        child: child,
      ),
    );
  }
}

/// Grille fine de 24px, très faiblement opaque.
class _GrilleHudPainter extends CustomPainter {
  static const double _pas = 24;

  @override
  void paint(Canvas canvas, Size size) {
    final trait = Paint()
      ..color = Colors.white.withValues(alpha: 0.025)
      ..strokeWidth = 1;
    for (double x = 0; x <= size.width; x += _pas) {
      canvas.drawLine(Offset(x, 0), Offset(x, size.height), trait);
    }
    for (double y = 0; y <= size.height; y += _pas) {
      canvas.drawLine(Offset(0, y), Offset(size.width, y), trait);
    }
  }

  @override
  bool shouldRepaint(covariant _GrilleHudPainter oldDelegate) => false;
}
