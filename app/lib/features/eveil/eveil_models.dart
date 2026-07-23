import '../accueil/home_models.dart';

/// Données révélées à la fin de l'onboarding, renvoyées par le backend à la
/// création du Chasseur : son rang de départ et ses stats initiales. L'app les
/// affiche, elle ne les calcule pas.
class EveilData {
  const EveilData({required this.rang, required this.stats});

  final String rang;
  final List<Stat> stats;
}
