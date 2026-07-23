import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../l10n/textes.dart';
import '../../theme/arise_theme.dart';
import '../../widgets/hexagon.dart';
import '../../widgets/hud_background.dart';
import '../../widgets/system_label.dart';
import '../accueil/home_models.dart';
import 'eveil_providers.dart';

/// Écran d'Éveil — la révélation du Chasseur à la fin de l'onboarding.
///
/// Affiche le rang de départ et les stats initiales renvoyés par le backend.
/// Le halo radial est plus intense qu'ailleurs : c'est un moment héros.
class EveilScreen extends ConsumerWidget {
  const EveilScreen({required this.onEntrer, super.key});

  /// Action déclenchée par « Entrer dans le Système » (navigation : à la charge
  /// de l'appelant).
  final VoidCallback onEntrer;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final data = ref.watch(eveilDataProvider);
    return Scaffold(
      body: HudBackground(
        child: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 32),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const SystemLabel(Textes.systeme, accent: AriseColors.glow),
                  const SizedBox(height: 10),
                  Text(
                    Textes.eveilTitre,
                    textAlign: TextAlign.center,
                    style: AriseTypography.titre.copyWith(
                      fontSize: 27,
                      letterSpacing: 1,
                    ),
                  ),
                  const SizedBox(height: 32),
                  HexBadge(lettre: data.rang, taille: 120),
                  const SizedBox(height: 24),
                  Text(
                    Textes.chasseurCree(data.rang),
                    textAlign: TextAlign.center,
                    style: AriseTypography.corps
                        .copyWith(color: AriseColors.texteAttenue),
                  ),
                  const SizedBox(height: 20),
                  _StatsDepart(stats: data.stats),
                  const SizedBox(height: 40),
                  FilledButton(
                    onPressed: onEntrer,
                    style: FilledButton.styleFrom(
                      backgroundColor: AriseColors.systeme,
                      foregroundColor: AriseColors.fond,
                      shape: const RoundedRectangleBorder(),
                      padding: const EdgeInsets.symmetric(
                        horizontal: 32,
                        vertical: 14,
                      ),
                    ),
                    child: Text(
                      Textes.entrerDansLeSysteme,
                      style: AriseTypography.corps.copyWith(
                        color: AriseColors.fond,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _StatsDepart extends StatelessWidget {
  const _StatsDepart({required this.stats});

  final List<Stat> stats;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        for (final stat in stats)
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 8),
            child: Column(
              children: [
                Text(
                  '${stat.valeur}',
                  style: AriseTypography.titre.copyWith(
                    fontSize: 23,
                    color: stat.domaine.couleur,
                    shadows: [
                      Shadow(
                        color: stat.domaine.couleur.withValues(alpha: 0.4),
                        blurRadius: 10,
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 3),
                SystemLabel(stat.libelle),
              ],
            ),
          ),
      ],
    );
  }
}
