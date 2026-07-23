import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'features/accueil/home_screen.dart';
import 'theme/arise_theme.dart';

void main() {
  runApp(const ProviderScope(child: AriseApp()));
}

/// Racine de l'application ARISE.
class AriseApp extends StatelessWidget {
  const AriseApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'ARISE',
      debugShowCheckedModeBanner: false,
      theme: ariseTheme(),
      home: const HomeScreen(),
    );
  }
}
