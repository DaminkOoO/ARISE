import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../l10n/textes.dart';
import '../../theme/arise_theme.dart';
import '../../widgets/etats_async.dart';
import '../../widgets/hud_background.dart';
import '../../widgets/system_label.dart';
import '../../widgets/system_panel.dart';
import 'habitudes_models.dart';
import 'habitudes_providers.dart';

/// Écran Habitudes & Tâches — les intentions qui reviennent, et ce qu'il reste
/// à faire une fois.
///
/// Consomme [habitudesDataProvider] et déballe ses trois états. Les séries
/// affichées sont celles que le backend annonce ; rien n'est recalculé ici.
class HabitudesScreen extends ConsumerWidget {
  const HabitudesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(habitudesDataProvider);
    return Scaffold(
      body: HudBackground(
        accent: AriseColors.habitudes,
        child: SafeArea(
          child: async.when(
            loading: () => const EtatChargement(),
            error: (_, _) => EtatErreur(
              onReessayer: () => ref.invalidate(habitudesDataProvider),
            ),
            data: (data) => _Contenu(data: data),
          ),
        ),
      ),
      floatingActionButton: async.hasValue ? const _BoutonAjouter() : null,
    );
  }
}

class _Contenu extends StatelessWidget {
  const _Contenu({required this.data});

  final HabitudesData data;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(
        AriseSpacing.ecran,
        26,
        AriseSpacing.ecran,
        // De quoi faire défiler la dernière ligne au-delà du bouton d'ajout,
        // qui flotte par-dessus la liste.
        88,
      ),
      children: [
        Text(Textes.habitudesTitre, style: AriseTypography.titre),
        const SizedBox(height: 16),
        const SystemLabel(
          Textes.sectionHabitudes,
          accent: AriseColors.habitudes,
        ),
        const SizedBox(height: 8),
        if (data.habitudes.isEmpty)
          const _ListeVide(message: Textes.aucuneHabitude)
        else
          for (final habitude in data.habitudes) ...[
            _LigneHabitude(habitude: habitude),
            const SizedBox(height: 8),
          ],
        const SizedBox(height: 16),
        const SystemLabel(Textes.sectionTaches, accent: AriseColors.habitudes),
        const SizedBox(height: 8),
        if (data.taches.isEmpty)
          const _ListeVide(message: Textes.aucuneTache)
        else
          for (final tache in data.taches) ...[
            _LigneTache(tache: tache),
            const SizedBox(height: 8),
          ],
      ],
    );
  }
}

/// Une habitude : son nom, l'état de la période en cours, et sa série.
///
/// La carte n'est **pas** teintée en rouge quand l'habitude n'est pas encore
/// tenue : le jour n'est pas fini, et signaler un manquement à quelqu'un qui a
/// encore le temps de le tenir est exactement ce que la règle n°5 interdit.
class _LigneHabitude extends StatelessWidget {
  const _LigneHabitude({required this.habitude});

  final Habitude habitude;

  @override
  Widget build(BuildContext context) {
    final tenue = habitude.tenueAujourdHui;
    return SystemPanel(
      accent: AriseColors.habitudes,
      coinsHud: tenue,
      bordureAccent: tenue,
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      child: Row(
        children: [
          _Marqueur(cochee: tenue),
          const SizedBox(width: 12),
          Expanded(child: Text(habitude.nom, style: AriseTypography.corps)),
          const SizedBox(width: 10),
          _Serie(habitude: habitude),
        ],
      ),
    );
  }
}

/// La série, dans l'unité de son rythme. Rajdhani : c'est un chiffre, donc un
/// élément fort de la charte, et non une métadonnée.
class _Serie extends StatelessWidget {
  const _Serie({required this.habitude});

  final Habitude habitude;

  @override
  Widget build(BuildContext context) {
    if (habitude.serie == 0) {
      return const SystemLabel(Textes.serieACommencer);
    }

    final libelle = switch (habitude.rythme) {
      RythmeHabitude.quotidienne => Textes.serieEnJours(habitude.serie),
      RythmeHabitude.hebdomadaire => Textes.serieEnSemaines(habitude.serie),
    };

    return Text(
      libelle,
      style: AriseTypography.titre.copyWith(
        fontSize: 15,
        color: AriseColors.habitudes,
        shadows: [
          BoxShadow(
            color: AriseColors.habitudes.withValues(alpha: 0.4),
            blurRadius: 14,
          ),
        ],
      ),
    );
  }
}

class _LigneTache extends StatelessWidget {
  const _LigneTache({required this.tache});

  final Tache tache;

  @override
  Widget build(BuildContext context) {
    return SystemPanel(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      child: Row(
        children: [
          const _Marqueur(cochee: false),
          const SizedBox(width: 12),
          Expanded(child: Text(tache.titre, style: AriseTypography.corps)),
          const SizedBox(width: 10),
          // Une tâche sans échéance n'est pas en retard : on le dit, plutôt que
          // d'inventer une date ou de laisser un blanc qu'on lirait comme un
          // oubli.
          SystemLabel(tache.echeance ?? Textes.sansEcheance),
        ],
      ),
    );
  }
}

/// La case d'une ligne : pleine et lumineuse une fois tenue, simple contour
/// sinon. Jamais de croix ni de rouge — l'absence n'est pas une faute.
class _Marqueur extends StatelessWidget {
  const _Marqueur({required this.cochee});

  final bool cochee;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 18,
      height: 18,
      decoration: BoxDecoration(
        color: cochee ? AriseColors.habitudes : Colors.transparent,
        border: Border.all(
          color: cochee ? AriseColors.habitudes : AriseColors.bordure,
          width: 1.5,
        ),
        boxShadow: cochee
            ? [
                BoxShadow(
                  color: AriseColors.habitudes.withValues(alpha: 0.45),
                  blurRadius: 12,
                ),
              ]
            : null,
      ),
      child: cochee
          ? const Icon(Icons.check, size: 13, color: AriseColors.fond)
          : null,
    );
  }
}

class _ListeVide extends StatelessWidget {
  const _ListeVide({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return SystemPanel(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 16),
      child: Text(
        message,
        style: AriseTypography.corps.copyWith(color: AriseColors.texteAttenue),
      ),
    );
  }
}

/// Le bouton d'ajout.
///
/// Sans action pour l'instant, comme le « Terminer » de l'écran Sport : l'API
/// n'expose encore aucun endpoint Habitudes ni Tâches, et ouvrir un formulaire
/// qui ne peut rien enregistrer tromperait le Chasseur plus sûrement qu'un
/// bouton inerte.
class _BoutonAjouter extends StatelessWidget {
  const _BoutonAjouter();

  @override
  Widget build(BuildContext context) {
    return FloatingActionButton(
      onPressed: () {},
      backgroundColor: AriseColors.habitudes,
      foregroundColor: AriseColors.fond,
      shape: const RoundedRectangleBorder(),
      tooltip: Textes.ajouter,
      child: const Icon(Icons.add),
    );
  }
}
