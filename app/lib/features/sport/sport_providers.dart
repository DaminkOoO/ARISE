import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'sport_models.dart';
import 'sport_service.dart';

/// Service Sport injectable, remplacé par un double en test.
final sportServiceProvider = Provider<SportService>(
  (ref) => const DemoSportService(),
);

/// Données Sport en trois états (chargement, donnée, erreur).
final sportDataProvider = FutureProvider<SportData>(
  (ref) => ref.watch(sportServiceProvider).chargerSport(),
);
