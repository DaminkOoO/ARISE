import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'onglet_arise.dart';

/// L'onglet ouvert. Porté par un provider et non par l'état local de la
/// coquille : c'est de l'état applicatif, et un écran voudra un jour emmener le
/// Chasseur ailleurs — « voir mes habitudes » depuis le rapport du Système —
/// sans avoir à remonter jusqu'au widget parent.
class OngletActif extends Notifier<OngletArise> {
  @override
  OngletArise build() => OngletArise.accueil;

  void ouvrir(OngletArise onglet) => state = onglet;
}

final ongletActifProvider =
    NotifierProvider<OngletActif, OngletArise>(OngletActif.new);
