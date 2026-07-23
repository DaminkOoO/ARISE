import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'home_models.dart';
import 'home_service.dart';

/// Service de données de l'accueil, injectable. Un test le remplace par un
/// double via `overrideWithValue`.
final homeServiceProvider = Provider<HomeService>(
  (ref) => const DemoHomeService(),
);

/// Données de l'accueil, exposées en trois états (chargement, donnée, erreur)
/// via l'AsyncValue du FutureProvider.
final homeDataProvider = FutureProvider<HomeData>(
  (ref) => ref.watch(homeServiceProvider).chargerAccueil(),
);
