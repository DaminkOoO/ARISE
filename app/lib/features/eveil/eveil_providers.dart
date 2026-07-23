import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../accueil/home_models.dart';
import 'eveil_models.dart';

/// Résultat de l'éveil du Chasseur. Provider injectable : en production, il
/// portera la réponse de création du backend ; en test, un double le remplace.
/// Tant que le moteur central n'est pas branché, il sert les valeurs de départ
/// de la référence (rang E, stats à 10).
final eveilDataProvider = Provider<EveilData>(
  (ref) => const EveilData(
    rang: 'E',
    stats: [
      Stat(libelle: 'FOR', valeur: 10, domaine: Domaine.sport),
      Stat(libelle: 'VIT', valeur: 10, domaine: Domaine.sport),
      Stat(libelle: 'INT', valeur: 10, domaine: Domaine.habitudes),
      Stat(libelle: 'OR', valeur: 10, domaine: Domaine.budget),
      Stat(libelle: 'PER', valeur: 10, domaine: Domaine.calendrier),
    ],
  ),
);
