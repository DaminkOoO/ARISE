import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'onboarding_models.dart';

/// État local de sélection de l'objectif à l'onboarding. Sport est présélectionné,
/// comme dans la référence.
class ObjectifNotifier extends Notifier<Objectif> {
  @override
  Objectif build() => Objectif.sport;

  void selectionner(Objectif objectif) => state = objectif;
}

final objectifSelectionneProvider =
    NotifierProvider<ObjectifNotifier, Objectif>(ObjectifNotifier.new);
