import 'package:flutter/material.dart';

import '../features/navigation/onglet_arise.dart';
import '../theme/arise_theme.dart';

/// La barre des cinq domaines.
///
/// Vit ici et non dans un écran : elle appartient à la coquille de navigation,
/// qui est au-dessus de tous les écrans. Tant qu'elle était rendue par l'accueil,
/// elle ne pouvait mener nulle part — un écran ne peut pas se remplacer
/// lui-même.
class BarreNavigation extends StatelessWidget {
  const BarreNavigation({
    required this.actif,
    required this.onOuvrir,
    super.key,
  });

  final OngletArise actif;
  final ValueChanged<OngletArise> onOuvrir;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: const BoxDecoration(
        color: AriseColors.panneau,
        border: Border(top: BorderSide(color: AriseColors.bordure)),
      ),
      padding: const EdgeInsets.fromLTRB(8, 10, 8, 16),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceAround,
        children: [
          for (final onglet in OngletArise.values)
            _Onglet(
              onglet: onglet,
              actif: onglet == actif,
              onTap: () => onOuvrir(onglet),
            ),
        ],
      ),
    );
  }
}

class _Onglet extends StatelessWidget {
  const _Onglet({
    required this.onglet,
    required this.actif,
    required this.onTap,
  });

  final OngletArise onglet;
  final bool actif;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    // L'onglet actif prend l'accent de son domaine ; les autres restent
    // atténués. C'est la lueur, et non un fond plein, qui signale l'actif.
    final couleur = actif ? onglet.accent : AriseColors.texteAttenue;

    // GestureDetector et non InkWell : une onde d'encre Material sur une barre
    // HUD sombre est hors charte, et InkWell exigerait en prime un ancêtre
    // Material que la coquille n'a pas à introduire pour une barre.
    //
    // `opaque` pour que le tap porte sur toute la cible, pas seulement sur les
    // pixels peints du carré et du libellé.
    return GestureDetector(
      onTap: onTap,
      behavior: HitTestBehavior.opaque,
      // Une cible de tap large : la barre est en bas de l'écran, là où le pouce
      // vise mal.
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 18,
              height: 18,
              decoration: BoxDecoration(
                border: Border.all(color: couleur, width: 1.5),
                boxShadow: actif
                    ? [
                        BoxShadow(
                          color: couleur.withValues(alpha: 0.6),
                          blurRadius: 8,
                        ),
                      ]
                    : null,
              ),
            ),
            const SizedBox(height: 4),
            Text(
              onglet.libelle,
              style: AriseTypography.corps.copyWith(
                fontSize: 9.5,
                fontWeight: FontWeight.w500,
                color: couleur,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
