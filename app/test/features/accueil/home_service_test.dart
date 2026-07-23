import 'package:flutter_test/flutter_test.dart';

import 'package:arise/features/accueil/home_models.dart';
import 'package:arise/features/accueil/home_service.dart';

void main() {
  group('DemoHomeService', () {
    test('renvoie le Chasseur de démonstration de la référence', () async {
      final data = await DemoHomeService().chargerAccueil();

      expect(data.chasseur.nom, 'KAEL MORGAN');
      expect(data.chasseur.niveau, 14);
      expect(data.chasseur.rang, 'D');
      expect(data.chasseur.serieJours, 12);
    });

    test('expose cinq stats couvrant FOR, VIT, INT, OR, PER', () async {
      final data = await DemoHomeService().chargerAccueil();

      expect(data.stats.map((s) => s.libelle).toList(),
          ['FOR', 'VIT', 'INT', 'OR', 'PER']);
      expect(data.stats.first.valeur, 72);
    });

    test('porte le rapport du Système avec son horodatage', () async {
      final data = await DemoHomeService().chargerAccueil();

      expect(data.rapport.message, contains('quêtes'));
      expect(data.rapport.horodatage, isNotEmpty);
    });

    test('liste les quêtes du jour avec domaine, XP et état', () async {
      final data = await DemoHomeService().chargerAccueil();

      expect(data.quetes, hasLength(3));
      final premiere = data.quetes.first;
      expect(premiere.titre, contains('Épreuve du Jour'));
      expect(premiere.domaine, Domaine.sport);
      expect(premiere.xp, 30);
      expect(premiere.terminee, isFalse);
      expect(data.quetes.last.terminee, isTrue);
    });
  });

  test('chaque domaine porte sa couleur de charte', () {
    expect(Domaine.sport.couleur.toARGB32(), 0xFFE63946);
    expect(Domaine.budget.couleur.toARGB32(), 0xFFF2B705);
    expect(Domaine.habitudes.couleur.toARGB32(), 0xFF2DC653);
    expect(Domaine.calendrier.couleur.toARGB32(), 0xFF9D4EDD);
  });
}
