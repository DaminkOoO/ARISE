import 'package:flutter/material.dart';

import '../l10n/textes.dart';
import '../theme/arise_theme.dart';
import 'system_label.dart';

/// État de chargement partagé : indicateur + étiquette « Synchronisation avec
/// le Système… ». Un écran de données ne laisse jamais un blanc.
class EtatChargement extends StatelessWidget {
  const EtatChargement({super.key});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: const [
          CircularProgressIndicator(color: AriseColors.systeme),
          SizedBox(height: 16),
          SystemLabel(Textes.synchronisation, accent: AriseColors.glow),
        ],
      ),
    );
  }
}

/// État d'erreur partagé : message français non culpabilisant + Réessayer.
class EtatErreur extends StatelessWidget {
  const EtatErreur({required this.onReessayer, super.key});

  final VoidCallback onReessayer;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(AriseSpacing.ecran),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const SystemLabel(Textes.systeme, accent: AriseColors.glow),
            const SizedBox(height: 12),
            Text(
              Textes.erreurSysteme,
              textAlign: TextAlign.center,
              style: AriseTypography.corps,
            ),
            const SizedBox(height: 20),
            OutlinedButton(
              onPressed: onReessayer,
              child: Text(Textes.reessayer, style: AriseTypography.corps),
            ),
          ],
        ),
      ),
    );
  }
}
