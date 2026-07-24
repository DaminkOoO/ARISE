# Prompt de correction — ARISE (à coller dans Claude Design)

Les 8 écrans existants suivent la bonne structure et les bonnes couleurs, mais le rendu est
trop plat et générique ("dashboard SaaS sombre" plutôt que "fenêtre du Système"). Corrige les
4 points suivants sur tous les écrans existants, sans changer la structure ni le contenu déjà
en place :

## 1. Contraste typographique (le problème principal)
Actuellement une seule police sans-serif est utilisée partout — c'est ce qui rend le résultat
générique. Applique strictement 3 rôles distincts :
- **Chiffres et titres** (nom du Chasseur, niveau, XP, valeurs de stats, montants en €) :
  **Rajdhani, graisse 700**, letter-spacing léger (0.5–1px). Ces éléments doivent être visiblement
  plus imposants et plus "techniques" que le reste — augmente leur taille si besoin pour créer
  un vrai contraste avec le texte courant.
- **Texte courant** (descriptions de quêtes, messages du Système, corps de texte) : **Inter,
  graisse 400–500**. Reste discret, ne rentre pas en compétition avec les titres.
- **Étiquettes système et métadonnées** (`[RAPPORT DU SYSTÈME]`, `[QUÊTE DU JOUR]`, timestamps,
  labels de stats FOR/VIT/INT/OR/PER) : **JetBrains Mono**, tout en majuscules, taille 9–10px,
  letter-spacing 1–2px, couleur `--text-dim` (#8B98A8).

## 2. Signature visuelle : coins façon viseur HUD
Chaque carte/panneau principal (rapport quotidien, badge de rang, cartes de quêtes/transactions)
doit avoir 4 petits crochets en L aux coins — pas de bordure arrondie classique. Technique :
4 petits éléments de 12–14px en forme de L, épaisseur 2px, couleur d'accent du panneau, positionnés
aux 4 coins en léger débord (comme un viseur de jeu vidéo). C'est l'élément signature du design,
il doit apparaître de façon cohérente sur TOUS les écrans, pas seulement l'accueil.

## 3. Lueur (glow) réelle
Rien ne doit paraître plat. Ajoute un box-shadow lumineux (blur 15–25px, faible opacité ~30-45%,
couleur = couleur d'accent de l'élément) sur : le badge de rang hexagonal, les barres de
statistiques remplies, la bordure de la carte "Rapport Quotidien", et tout élément actif dans
la barre de navigation. Sans cette lueur, l'interface ne se distingue pas d'un dashboard classique.

## 4. Texture de fond
Remplace le fond uni noir/bleu-nuit par : un dégradé radial très subtil (lueur bleue ~10-15%
d'opacité) centré en haut de l'écran, superposé à une grille très fine (lignes à 1-2% d'opacité,
espacées de ~24px) — effet "grille HUD" discret, jamais distrayant.

## Hiérarchie à renforcer en plus de ces 4 points
Les éléments "héros" (nom + rang du Chasseur, carte Rapport Quotidien, badge hexagonal) doivent
visuellement dominer la page — plus grands, plus lumineux, traitement plus riche que les éléments
de liste (quêtes, transactions, habitudes) qui doivent rester compacts et sobres. Actuellement
tout a le même poids visuel, ce qui contribue à l'effet "template".

## Référence de couleurs (déjà correctes, à conserver)
Système/neutre : `#3A86FF` → glow `#4CC9F0` · Sport : `#E63946` · Budget : `#F2B705` ·
Habitudes : `#2DC653` · Calendrier : `#9D4EDD` · Fond : `#05070C` · Panneaux : `#0D1420`,
bordure `#1E293B`.

Applique ces 4 corrections de façon cohérente sur les 8 écrans déjà générés (Accueil, Fenêtre de
Statut, Sport, Budget, Habitudes & Tâches, Calendrier, Onboarding/Éveil, Coach) avant de proposer
toute nouvelle variation.
