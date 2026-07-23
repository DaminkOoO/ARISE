import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../l10n/textes.dart';
import '../../theme/arise_theme.dart';
import '../../widgets/hud_corners.dart';
import '../../widgets/system_label.dart';
import '../../widgets/system_panel.dart';
import 'auth_models.dart';
import 'auth_providers.dart';

/// Écran d'entrée : connexion ou inscription du Chasseur. Sobre, coins HUD,
/// tutoiement. Conçu selon la charte (la référence n'a pas d'écran dédié).
class AuthScreen extends ConsumerStatefulWidget {
  const AuthScreen({required this.onAuthentifie, super.key});

  /// Remonte le JWT obtenu (navigation vers l'app : à l'appelant).
  final ValueChanged<String> onAuthentifie;

  @override
  ConsumerState<AuthScreen> createState() => _AuthScreenState();
}

class _AuthScreenState extends ConsumerState<AuthScreen> {
  final _nom = TextEditingController();
  final _motDePasse = TextEditingController();
  AuthMode _mode = AuthMode.connexion;
  String? _erreurNom;
  String? _erreurMotDePasse;

  @override
  void dispose() {
    _nom.dispose();
    _motDePasse.dispose();
    super.dispose();
  }

  void _basculer() {
    setState(() {
      _mode = _mode == AuthMode.connexion
          ? AuthMode.inscription
          : AuthMode.connexion;
      _erreurNom = null;
      _erreurMotDePasse = null;
    });
  }

  void _soumettre() {
    final nom = _nom.text.trim();
    final mdp = _motDePasse.text;
    setState(() {
      _erreurNom = nom.isEmpty ? Textes.nomChasseurVide : null;
      _erreurMotDePasse = mdp.isEmpty ? Textes.motDePasseVide : null;
    });
    if (nom.isEmpty || mdp.isEmpty) return;
    ref.read(authControllerProvider.notifier).soumettre(_mode, nom, mdp);
  }

  @override
  Widget build(BuildContext context) {
    final etat = ref.watch(authControllerProvider);

    ref.listen<AsyncValue<String?>>(authControllerProvider, (_, suivant) {
      final token = suivant.valueOrNull;
      if (suivant is AsyncData && token != null) {
        widget.onAuthentifie(token);
      }
    });

    final estConnexion = _mode == AuthMode.connexion;
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: 28, vertical: 32),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const SystemLabel(Textes.systeme, accent: AriseColors.glow),
                const SizedBox(height: 10),
                Text(
                  'ARISE',
                  textAlign: TextAlign.center,
                  style: AriseTypography.titre.copyWith(
                    fontSize: 40,
                    letterSpacing: 2,
                    shadows: const [
                      Shadow(color: AriseColors.glow, blurRadius: 20),
                    ],
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  Textes.authAccroche,
                  textAlign: TextAlign.center,
                  style: AriseTypography.corps
                      .copyWith(color: AriseColors.texteAttenue),
                ),
                const SizedBox(height: 28),
                HudCorners(
                  accent: AriseColors.systeme,
                  debord: 2,
                  child: SystemPanel(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Text(
                          estConnexion ? Textes.connexion : Textes.inscription,
                          style: AriseTypography.titre.copyWith(fontSize: 19),
                        ),
                        const SizedBox(height: 16),
                        _Champ(
                          cle: const Key('champ-nom'),
                          controleur: _nom,
                          libelle: Textes.nomChasseur,
                          erreur: _erreurNom,
                        ),
                        const SizedBox(height: 14),
                        _Champ(
                          cle: const Key('champ-mot-de-passe'),
                          controleur: _motDePasse,
                          libelle: Textes.motDePasse,
                          erreur: _erreurMotDePasse,
                          masque: true,
                        ),
                        if (etat is AsyncError) ...[
                          const SizedBox(height: 14),
                          Text(
                            Textes.identifiantsRefuses,
                            style: AriseTypography.corps
                                .copyWith(color: AriseColors.sport),
                          ),
                        ],
                        const SizedBox(height: 20),
                        FilledButton(
                          key: const Key('bouton-soumettre'),
                          onPressed: etat is AsyncLoading ? null : _soumettre,
                          style: FilledButton.styleFrom(
                            backgroundColor: AriseColors.systeme,
                            shape: const RoundedRectangleBorder(),
                            padding: const EdgeInsets.symmetric(vertical: 14),
                          ),
                          child: etat is AsyncLoading
                              ? const SizedBox(
                                  height: 18,
                                  width: 18,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                    color: AriseColors.fond,
                                  ),
                                )
                              : Text(
                                  estConnexion
                                      ? Textes.seConnecter
                                      : Textes.seEveiller,
                                  style: AriseTypography.corps.copyWith(
                                    color: AriseColors.fond,
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: 16),
                TextButton(
                  onPressed: _basculer,
                  child: Text(
                    estConnexion ? Textes.sInscrire : Textes.jaiDejaUnCompte,
                    style: AriseTypography.corps
                        .copyWith(color: AriseColors.glow),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _Champ extends StatelessWidget {
  const _Champ({
    required this.cle,
    required this.controleur,
    required this.libelle,
    this.erreur,
    this.masque = false,
  });

  final Key cle;
  final TextEditingController controleur;
  final String libelle;
  final String? erreur;
  final bool masque;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SystemLabel(libelle),
        const SizedBox(height: 6),
        TextField(
          key: cle,
          controller: controleur,
          obscureText: masque,
          style: AriseTypography.corps,
          decoration: InputDecoration(
            isDense: true,
            filled: true,
            fillColor: AriseColors.fond,
            enabledBorder: const OutlineInputBorder(
              borderRadius: BorderRadius.zero,
              borderSide: BorderSide(color: AriseColors.bordure),
            ),
            focusedBorder: const OutlineInputBorder(
              borderRadius: BorderRadius.zero,
              borderSide: BorderSide(color: AriseColors.systeme),
            ),
          ),
        ),
        if (erreur != null) ...[
          const SizedBox(height: 6),
          Text(
            erreur!,
            style: AriseTypography.corps
                .copyWith(fontSize: 12, color: AriseColors.sport),
          ),
        ],
      ],
    );
  }
}
