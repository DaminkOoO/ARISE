import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../l10n/textes.dart';
import '../../theme/arise_theme.dart';
import '../../widgets/hexagon.dart';
import '../../widgets/hud_background.dart';
import '../../widgets/hud_corners.dart';
import '../../widgets/system_label.dart';
import '../../widgets/system_panel.dart';
import '../../widgets/stat_bar.dart';
import 'home_models.dart';
import 'home_providers.dart';

/// Écran Accueil — la « fenêtre du Système » du Chasseur.
///
/// Consomme [homeDataProvider] et déballe explicitement ses trois états :
/// chargement, donnée, erreur. Toutes les valeurs viennent de l'API : l'app
/// les affiche, elle ne recalcule ni niveau, ni rang, ni série.
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(homeDataProvider);
    return Scaffold(
      body: HudBackground(
        child: SafeArea(
          child: async.when(
            loading: () => const _EtatChargement(),
            error: (_, _) => _EtatErreur(
              onReessayer: () => ref.invalidate(homeDataProvider),
            ),
            data: (data) => _Contenu(data: data),
          ),
        ),
      ),
    );
  }
}

class _EtatChargement extends StatelessWidget {
  const _EtatChargement();

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

class _EtatErreur extends StatelessWidget {
  const _EtatErreur({required this.onReessayer});

  final VoidCallback onReessayer;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(AriseSpacing.ecran),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const SystemLabel('[Système]', accent: AriseColors.glow),
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

class _Contenu extends StatelessWidget {
  const _Contenu({required this.data});

  final HomeData data;

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
        _EnTete(chasseur: data.chasseur),
        const SizedBox(height: 16),
        _CarteRapport(rapport: data.rapport),
        const SizedBox(height: 16),
        for (final stat in data.stats) ...[
          StatBar(
            libelle: stat.libelle,
            valeur: stat.valeur,
            accent: stat.domaine.couleur,
          ),
          const SizedBox(height: 9),
        ],
        const SizedBox(height: 7),
        _Serie(jours: data.chasseur.serieJours),
        const SizedBox(height: 16),
        const SystemLabel(Textes.quetesDuJour),
        const SizedBox(height: 8),
        for (final quete in data.quetes) ...[
          _LigneQuete(quete: quete),
          const SizedBox(height: 8),
        ],
      ],
    );
  }
}

class _EnTete extends StatelessWidget {
  const _EnTete({required this.chasseur});

  final Chasseur chasseur;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const SystemLabel(Textes.chasseur),
              const SizedBox(height: 4),
              Text(
                chasseur.nom,
                style: AriseTypography.titre.copyWith(
                  fontSize: 27,
                  shadows: [
                    const Shadow(
                      color: AriseColors.glow,
                      blurRadius: 18,
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
        HudCorners(
          accent: AriseColors.systeme,
          child: Padding(
            padding: const EdgeInsets.all(9),
            child: Row(
              children: [
                _BadgeNiveau(niveau: chasseur.niveau),
                const SizedBox(width: 12),
                _BadgeRang(rang: chasseur.rang),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _BadgeNiveau extends StatelessWidget {
  const _BadgeNiveau({required this.niveau});

  final int niveau;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 64,
      height: 64,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        border: Border.all(color: AriseColors.systeme, width: 3),
        boxShadow: [
          BoxShadow(
            color: AriseColors.systeme.withValues(alpha: 0.4),
            blurRadius: 18,
          ),
        ],
      ),
      child: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              '$niveau',
              style: AriseTypography.titre.copyWith(fontSize: 19, height: 1),
            ),
            const SystemLabel(Textes.niveauCourt),
          ],
        ),
      ),
    );
  }
}

class _BadgeRang extends StatelessWidget {
  const _BadgeRang({required this.rang});

  final String rang;

  @override
  Widget build(BuildContext context) {
    return HexBadge(lettre: rang, taille: 48);
  }
}

class _CarteRapport extends StatelessWidget {
  const _CarteRapport({required this.rapport});

  final RapportSysteme rapport;

  @override
  Widget build(BuildContext context) {
    return SystemPanel(
      glow: true,
      coinsHud: true,
      bordureAccent: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const SystemLabel(Textes.rapportDuSysteme, accent: AriseColors.glow),
          const SizedBox(height: 10),
          Text(rapport.message, style: AriseTypography.corps),
          const SizedBox(height: 12),
          Align(
            alignment: Alignment.centerRight,
            child: SystemLabel(rapport.horodatage),
          ),
        ],
      ),
    );
  }
}

class _Serie extends StatelessWidget {
  const _Serie({required this.jours});

  final int jours;

  @override
  Widget build(BuildContext context) {
    return SystemPanel(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      child: Row(
        children: [
          const Icon(Icons.change_history, size: 10, color: AriseColors.budget),
          const SizedBox(width: 8),
          Text.rich(
            TextSpan(
              style: AriseTypography.corps,
              children: [
                TextSpan(text: '${Textes.serieActuelle} : '),
                TextSpan(
                  text: Textes.serieEnJours(jours),
                  style: AriseTypography.chiffreStat,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _LigneQuete extends StatelessWidget {
  const _LigneQuete({required this.quete});

  final Quete quete;

  @override
  Widget build(BuildContext context) {
    final accent = quete.domaine.couleur;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: AriseColors.panneau,
        border: Border(
          top: const BorderSide(color: AriseColors.bordure),
          bottom: const BorderSide(color: AriseColors.bordure),
          right: const BorderSide(color: AriseColors.bordure),
          left: BorderSide(color: accent, width: 3),
        ),
      ),
      child: Row(
        children: [
          _CaseQuete(terminee: quete.terminee),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              quete.titre,
              style: AriseTypography.corps.copyWith(
                decoration:
                    quete.terminee ? TextDecoration.lineThrough : null,
                color: quete.terminee ? AriseColors.texteAttenue : null,
              ),
            ),
          ),
          Text(
            Textes.xp(quete.xp),
            style: AriseTypography.chiffreStat.copyWith(color: accent),
          ),
        ],
      ),
    );
  }
}

class _CaseQuete extends StatelessWidget {
  const _CaseQuete({required this.terminee});

  final bool terminee;

  @override
  Widget build(BuildContext context) {
    if (terminee) {
      return Container(
        width: 16,
        height: 16,
        decoration: BoxDecoration(
          color: AriseColors.habitudes,
          borderRadius: BorderRadius.circular(3),
        ),
        child: const Icon(Icons.check, size: 12, color: AriseColors.fond),
      );
    }
    return Container(
      width: 16,
      height: 16,
      decoration: BoxDecoration(
        border: Border.all(color: AriseColors.texteAttenue, width: 1.5),
      ),
    );
  }
}
