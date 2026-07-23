import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:arise/theme/arise_theme.dart';

import '../support/fonts.dart';

void main() {
  setUpAll(initialiserBinding);

  group('AriseColors — jetons de la charte', () {
    test('porte les couleurs de domaine et de structure de la charte', () {
      expect(AriseColors.systeme, const Color(0xFF3A86FF));
      expect(AriseColors.glow, const Color(0xFF4CC9F0));
      expect(AriseColors.sport, const Color(0xFFE63946));
      expect(AriseColors.budget, const Color(0xFFF2B705));
      expect(AriseColors.habitudes, const Color(0xFF2DC653));
      expect(AriseColors.calendrier, const Color(0xFF9D4EDD));
      expect(AriseColors.fond, const Color(0xFF05070C));
      expect(AriseColors.panneau, const Color(0xFF0D1420));
      expect(AriseColors.bordure, const Color(0xFF1E293B));
      expect(AriseColors.texteAttenue, const Color(0xFF8B98A8));
    });
  });

  group('AriseTypography — trois rôles stricts', () {
    test('les titres/chiffres sont en Rajdhani 700 avec letter-spacing', () {
      final style = AriseTypography.titre;
      expect(style.fontWeight, FontWeight.w700);
      expect(style.letterSpacing, greaterThanOrEqualTo(0.5));
    });

    test('les étiquettes Système sont en petit corps espacé et atténué', () {
      final style = AriseTypography.etiquette;
      expect(style.letterSpacing, greaterThanOrEqualTo(1.0));
      expect(style.color, AriseColors.texteAttenue);
      expect(style.fontSize, lessThanOrEqualTo(10));
    });
  });

  group('ariseTheme', () {
    test('utilise le fond de la charte et une base sombre', () {
      final theme = ariseTheme();
      expect(theme.scaffoldBackgroundColor, AriseColors.fond);
      expect(theme.brightness, Brightness.dark);
    });
  });
}
