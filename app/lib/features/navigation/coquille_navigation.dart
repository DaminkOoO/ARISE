import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../l10n/textes.dart';
import '../../theme/arise_theme.dart';
import '../../widgets/barre_navigation.dart';
import '../../widgets/hud_background.dart';
import '../../widgets/system_panel.dart';
import '../accueil/home_screen.dart';
import '../habitudes/habitudes_screen.dart';
import '../sport/sport_screen.dart';
import 'navigation_providers.dart';
import 'onglet_arise.dart';

/// La coquille qui porte les écrans et la barre des domaines.
///
/// C'est elle qui rend les écrans atteignables : jusqu'ici, chacun existait et
/// était testé, mais aucun n'était référencé ailleurs que dans son propre
/// fichier — la barre était dessinée par l'accueil, et un écran ne peut pas se
/// remplacer lui-même.
///
/// [IndexedStack] plutôt qu'un simple `switch` : chaque écran garde son état
/// entre deux visites, donc sa position de défilement et ses données déjà
/// chargées. Revenir sur Sport ne relance pas un appel au Système.
class CoquilleNavigation extends ConsumerWidget {
  const CoquilleNavigation({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final actif = ref.watch(ongletActifProvider);

    return Column(
      children: [
        Expanded(
          child: IndexedStack(
            index: actif.index,
            children: const [
              HomeScreen(),
              SportScreen(),
              _DomaineAVenir(onglet: OngletArise.budget),
              HabitudesScreen(),
              _DomaineAVenir(onglet: OngletArise.calendrier),
            ],
          ),
        ),
        BarreNavigation(
          actif: actif,
          onOuvrir: (onglet) => ref.read(ongletActifProvider.notifier).ouvrir(onglet),
        ),
      ],
    );
  }
}

/// Un domaine dont l'écran n'existe pas encore (Budget en phase 3, Calendrier
/// en phase 4).
///
/// Le Système annonce ce qui vient plutôt que de laisser un écran vide, qu'on
/// lirait comme une panne — et le formule sans rien reprocher au Chasseur,
/// puisqu'il n'y est pour rien.
class _DomaineAVenir extends StatelessWidget {
  const _DomaineAVenir({required this.onglet});

  final OngletArise onglet;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: HudBackground(
        accent: onglet.accent,
        child: SafeArea(
          child: Center(
            child: Padding(
              padding: const EdgeInsets.all(AriseSpacing.ecran),
              child: SystemPanel(
                accent: onglet.accent,
                coinsHud: true,
                bordureAccent: true,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      onglet.libelle.toUpperCase(),
                      style: AriseTypography.titre.copyWith(
                        fontSize: 19,
                        color: onglet.accent,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      Textes.domaineAVenir,
                      style: AriseTypography.corps
                          .copyWith(color: AriseColors.texteAttenue),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
