import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../l10n/textes.dart';
import '../../theme/arise_theme.dart';
import '../../widgets/etats_async.dart';
import '../../widgets/hud_background.dart';
import '../../widgets/system_label.dart';
import '../../widgets/system_panel.dart';
import 'sport_models.dart';
import 'sport_providers.dart';

/// Écran Sport — quête du jour, stats de combat et historique récent.
///
/// Consomme [sportDataProvider] et déballe ses trois états. La quête est un
/// défi de jeu généré par le Système, jamais une prescription médicale.
class SportScreen extends ConsumerWidget {
  const SportScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(sportDataProvider);
    return Scaffold(
      body: HudBackground(
        accent: AriseColors.sport,
        child: SafeArea(
          child: async.when(
            loading: () => const EtatChargement(),
            error: (_, _) => EtatErreur(
              onReessayer: () => ref.invalidate(sportDataProvider),
            ),
            data: (data) => _Contenu(data: data),
          ),
        ),
      ),
    );
  }
}

class _Contenu extends StatelessWidget {
  const _Contenu({required this.data});

  final SportData data;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(
        AriseSpacing.ecran,
        26,
        AriseSpacing.ecran,
        24,
      ),
      children: [
        Text(Textes.sportTitre, style: AriseTypography.titre),
        const SizedBox(height: 16),
        _CarteQuete(quete: data.quete),
        const SizedBox(height: 16),
        Row(
          children: [
            for (var i = 0; i < data.stats.length; i++) ...[
              if (i > 0) const SizedBox(width: 12),
              Expanded(child: _CarteStat(stat: data.stats[i])),
            ],
          ],
        ),
        const SizedBox(height: 16),
        const SystemLabel(Textes.historiqueRecent),
        const SizedBox(height: 8),
        for (final seance in data.historique) ...[
          _LigneSeance(seance: seance),
          const SizedBox(height: 8),
        ],
      ],
    );
  }
}

class _CarteQuete extends StatelessWidget {
  const _CarteQuete({required this.quete});

  final QueteSport quete;

  @override
  Widget build(BuildContext context) {
    return SystemPanel(
      accent: AriseColors.sport,
      glow: true,
      coinsHud: true,
      bordureAccent: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const SystemLabel(Textes.queteDuJour, accent: AriseColors.sport),
          const SizedBox(height: 10),
          Text(quete.titre, style: AriseTypography.titre.copyWith(fontSize: 19)),
          const SizedBox(height: 5),
          Text(
            quete.description,
            style: AriseTypography.corps.copyWith(color: AriseColors.texteAttenue),
          ),
          const SizedBox(height: 16),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                Textes.xp(quete.xp),
                style: AriseTypography.titre
                    .copyWith(fontSize: 16, color: AriseColors.sport),
              ),
              FilledButton(
                onPressed: () {},
                style: FilledButton.styleFrom(
                  backgroundColor: AriseColors.sport,
                  shape: const RoundedRectangleBorder(),
                  padding:
                      const EdgeInsets.symmetric(horizontal: 20, vertical: 9),
                ),
                child: Text(
                  Textes.terminer,
                  style: AriseTypography.corps.copyWith(
                    color: AriseColors.fond,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _CarteStat extends StatelessWidget {
  const _CarteStat({required this.stat});

  final StatSport stat;

  @override
  Widget build(BuildContext context) {
    final fraction = (stat.valeur / 100).clamp(0.0, 1.0);
    return SystemPanel(
      padding: const EdgeInsets.all(14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SystemLabel(stat.libelle),
          const SizedBox(height: 7),
          Text(
            '${stat.valeur}',
            style: AriseTypography.titre.copyWith(
              fontSize: 36,
              color: AriseColors.sport,
              shadows: [
                BoxShadow(
                  color: AriseColors.sport.withValues(alpha: 0.4),
                  blurRadius: 16,
                ),
              ],
            ),
          ),
          const SizedBox(height: 7),
          SizedBox(
            height: 5,
            child: ColoredBox(
              color: AriseColors.bordure,
              child: Align(
                alignment: Alignment.centerLeft,
                child: FractionallySizedBox(
                  widthFactor: fraction,
                  child: Container(
                    decoration: BoxDecoration(
                      color: AriseColors.sport,
                      boxShadow: [
                        BoxShadow(
                          color: AriseColors.sport.withValues(alpha: 0.45),
                          blurRadius: 12,
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _LigneSeance extends StatelessWidget {
  const _LigneSeance({required this.seance});

  final SeanceSport seance;

  @override
  Widget build(BuildContext context) {
    return SystemPanel(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      child: Row(
        children: [
          Container(
            width: 16,
            height: 16,
            decoration: BoxDecoration(
              color: AriseColors.habitudes,
              borderRadius: BorderRadius.circular(3),
            ),
          ),
          const SizedBox(width: 10),
          Expanded(child: Text(seance.titre, style: AriseTypography.corps)),
          SystemLabel(seance.quand),
        ],
      ),
    );
  }
}
