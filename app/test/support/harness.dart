import 'package:flutter/material.dart';

import 'package:arise/theme/arise_theme.dart';

/// Enveloppe un widget dans un MaterialApp au thème ARISE pour les widget tests.
Widget enrober(Widget enfant) {
  return MaterialApp(
    debugShowCheckedModeBanner: false,
    theme: ariseTheme(),
    home: Scaffold(body: enfant),
  );
}
