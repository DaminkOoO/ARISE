import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'habitudes_models.dart';
import 'habitudes_service.dart';

/// Service Habitudes & Tâches injectable, remplacé par un double en test.
final habitudesServiceProvider = Provider<HabitudesService>(
  (ref) => const DemoHabitudesService(),
);

/// Données de l'écran en trois états (chargement, donnée, erreur).
final habitudesDataProvider = FutureProvider<HabitudesData>(
  (ref) => ref.watch(habitudesServiceProvider).chargerHabitudes(),
);
