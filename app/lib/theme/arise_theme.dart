import 'package:flutter/material.dart';

/// Jetons de design d'ARISE — « la fenêtre du Système ».
///
/// Source de vérité unique des couleurs, de la typographie et des espacements.
/// Aucun écran ne code une couleur ou une taille en dur : tout se réfère ici.
/// Voir la section « Charte visuelle » de CLAUDE.md et la référence pixel
/// `design/ARISE-Screens.reference.html`.

/// Jetons de couleur de la charte (acquis — à conserver).
abstract final class AriseColors {
  /// Accent Système / neutre.
  static const Color systeme = Color(0xFF3A86FF);

  /// Lueur cyan associée au Système.
  static const Color glow = Color(0xFF4CC9F0);

  /// Domaine Sport.
  static const Color sport = Color(0xFFE63946);

  /// Domaine Budget.
  static const Color budget = Color(0xFFF2B705);

  /// Domaine Habitudes & Tâches.
  static const Color habitudes = Color(0xFF2DC653);

  /// Domaine Calendrier.
  static const Color calendrier = Color(0xFF9D4EDD);

  /// Fond général de l'app.
  static const Color fond = Color(0xFF05070C);

  /// Fond des panneaux et cartes.
  static const Color panneau = Color(0xFF0D1420);

  /// Bordure discrète des panneaux.
  static const Color bordure = Color(0xFF1E293B);

  /// Texte principal clair.
  static const Color texte = Color(0xFFE6EDF3);

  /// Texte atténué (étiquettes JetBrains Mono, métadonnées).
  static const Color texteAttenue = Color(0xFF8B98A8);
}

/// Trois rôles typographiques stricts, jamais une seule police.
abstract final class AriseTypography {
  static const String familleTitre = 'Rajdhani';
  static const String familleCorps = 'Inter';
  static const String familleEtiquette = 'JetBrainsMono';

  /// Titres et chiffres (nom, niveau, XP, valeurs de stats) : Rajdhani 700.
  static const TextStyle titre = TextStyle(
    fontFamily: familleTitre,
    fontWeight: FontWeight.w700,
    fontSize: 23,
    letterSpacing: 0.5,
    color: AriseColors.texte,
  );

  /// Chiffre héros (badge de rang, grande valeur).
  static const TextStyle chiffreHeros = TextStyle(
    fontFamily: familleTitre,
    fontWeight: FontWeight.w700,
    fontSize: 42,
    letterSpacing: 0.5,
    color: AriseColors.texte,
  );

  /// Chiffre de stat compact.
  static const TextStyle chiffreStat = TextStyle(
    fontFamily: familleTitre,
    fontWeight: FontWeight.w700,
    fontSize: 14,
    letterSpacing: 0.5,
    color: AriseColors.texte,
  );

  /// Texte courant (messages du Système, descriptions) : Inter 400–500.
  static const TextStyle corps = TextStyle(
    fontFamily: familleCorps,
    fontWeight: FontWeight.w400,
    fontSize: 13,
    height: 1.5,
    color: AriseColors.texte,
  );

  /// Étiquettes Système et métadonnées : JetBrains Mono, MAJ, espacées.
  static const TextStyle etiquette = TextStyle(
    fontFamily: familleEtiquette,
    fontWeight: FontWeight.w400,
    fontSize: 10,
    letterSpacing: 1.5,
    color: AriseColors.texteAttenue,
  );
}

/// Espacements et cotes HUD récurrents (jamais en dur dans un écran).
abstract final class AriseSpacing {
  static const double ecran = 20;
  static const double panneau = 18;

  /// Taille d'un crochet de coin HUD.
  static const double coinHud = 13;

  /// Épaisseur d'un trait de coin HUD.
  static const double coinHudTrait = 2;
}

/// Thème Material sombre d'ARISE, adossé aux jetons ci-dessus.
ThemeData ariseTheme() {
  final base = ThemeData(brightness: Brightness.dark, useMaterial3: true);
  return base.copyWith(
    scaffoldBackgroundColor: AriseColors.fond,
    colorScheme: base.colorScheme.copyWith(
      primary: AriseColors.systeme,
      surface: AriseColors.panneau,
      onSurface: AriseColors.texte,
    ),
    textTheme: base.textTheme
        .apply(
          fontFamily: AriseTypography.familleCorps,
          bodyColor: AriseColors.texte,
          displayColor: AriseColors.texte,
        ),
  );
}
