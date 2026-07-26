import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'features/accueil/home_screen.dart';
import 'features/auth/auth_providers.dart';
import 'features/auth/auth_screen.dart';
import 'theme/arise_theme.dart';
import 'widgets/etats_async.dart';
import 'widgets/hud_background.dart';

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
      home: const PorteDEntree(),
    );
  }
}

/// Décide, au démarrage, où le Chasseur atterrit : l'accueil s'il a déjà un
/// jeton en stockage sécurisé, l'écran d'authentification sinon.
///
/// La lecture du Keystore est asynchrone : ses trois états sont déballés, car
/// un écran blanc le temps de lire le jeton serait un bug. Un stockage
/// illisible ramène à l'authentification — c'est la porte d'entrée, jamais une
/// impasse.
class PorteDEntree extends ConsumerWidget {
  const PorteDEntree({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return ref
        .watch(jetonStockeProvider)
        .when(
          loading: () => const Scaffold(
            body: HudBackground(child: SafeArea(child: EtatChargement())),
          ),
          error: (_, _) => const _EcranAuth(),
          data: (jeton) =>
              jeton == null ? const _EcranAuth() : const HomeScreen(),
        );
  }
}

/// L'écran d'auth, et ce qui suit une authentification réussie : l'accueil
/// remplace l'écran d'entrée dans la pile — revenir en arrière sur un
/// formulaire de connexion déjà honoré n'aurait pas de sens.
class _EcranAuth extends StatelessWidget {
  const _EcranAuth();

  @override
  Widget build(BuildContext context) {
    return AuthScreen(
      onAuthentifie: (_) => Navigator.of(context).pushReplacement(
        MaterialPageRoute<void>(builder: (_) => const HomeScreen()),
      ),
    );
  }
}
