import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../l10n/textes.dart';
import '../../theme/arise_theme.dart';
import '../../widgets/hud_background.dart';
import '../../widgets/system_label.dart';
import 'onboarding_models.dart';
import 'onboarding_providers.dart';

/// Écran d'onboarding — le Système demande l'objectif du Chasseur.
///
/// La sélection vit dans [objectifSelectionneProvider] (l'UI reste bête).
class OnboardingScreen extends ConsumerWidget {
  const OnboardingScreen({required this.onContinuer, super.key});

  /// Remonte l'objectif choisi (navigation vers l'Éveil : à l'appelant).
  final ValueChanged<Objectif> onContinuer;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final selection = ref.watch(objectifSelectionneProvider);
    return Scaffold(
      body: HudBackground(
        child: SafeArea(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 32),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const SystemLabel(Textes.systeme, accent: AriseColors.glow),
                const SizedBox(height: 10),
                Text(
                  Textes.objectifQuestion,
                  style: AriseTypography.titre.copyWith(fontSize: 25),
                ),
                const SizedBox(height: 6),
                Text(
                  Textes.objectifSousTitre,
                  style: AriseTypography.corps
                      .copyWith(color: AriseColors.texteAttenue),
                ),
                const SizedBox(height: 28),
                for (final objectif in Objectif.values) ...[
                  _OptionObjectif(
                    objectif: objectif,
                    selectionnee: objectif == selection,
                    onTap: () => ref
                        .read(objectifSelectionneProvider.notifier)
                        .selectionner(objectif),
                  ),
                  const SizedBox(height: 10),
                ],
                const Spacer(),
                SizedBox(
                  width: double.infinity,
                  child: FilledButton(
                    onPressed: () => onContinuer(selection),
                    style: FilledButton.styleFrom(
                      backgroundColor: AriseColors.systeme,
                      shape: const RoundedRectangleBorder(),
                      padding: const EdgeInsets.symmetric(vertical: 14),
                    ),
                    child: Text(
                      Textes.continuer,
                      style: AriseTypography.corps.copyWith(
                        color: AriseColors.fond,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _OptionObjectif extends StatelessWidget {
  const _OptionObjectif({
    required this.objectif,
    required this.selectionnee,
    required this.onTap,
  });

  final Objectif objectif;
  final bool selectionnee;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: AriseColors.panneau,
          border: Border.all(
            color: selectionnee ? objectif.couleur : AriseColors.bordure,
          ),
          boxShadow: selectionnee
              ? [
                  BoxShadow(
                    color: objectif.couleur.withValues(alpha: 0.35),
                    blurRadius: 16,
                  ),
                ]
              : null,
        ),
        child: Row(
          children: [
            Container(
              width: 10,
              height: 10,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: objectif.couleur,
                boxShadow: selectionnee
                    ? [
                        BoxShadow(
                          color: objectif.couleur.withValues(alpha: 0.6),
                          blurRadius: 8,
                        ),
                      ]
                    : null,
              ),
            ),
            const SizedBox(width: 12),
            Text(
              objectif.libelle,
              style: AriseTypography.corps.copyWith(
                fontSize: 14,
                fontWeight: FontWeight.w500,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
