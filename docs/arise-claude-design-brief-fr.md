# Design Brief: ARISE — App mobile de gestion de vie gamifiée

## Contexte
ARISE est une application mobile qui gamifie la vie réelle sur le thème "Solo Leveling" :
budget, sport, habitudes/tâches et calendrier sont unifiés dans un "Système" façon RPG. Une IA
(Gemini) joue le rôle du Système : elle génère des quêtes personnalisées, écrit un rapport
quotidien, et fait progresser un profil de "Chasseur" (Hunter) à travers rangs et statistiques.

**Toute l'interface doit être en français** — labels, boutons, messages système, contenu
d'exemple. Le nom de marque "ARISE" reste tel quel (mot anglais déjà adopté comme nom propre).

Ne pas copier d'éléments visuels ou de dialogues du manhwa/anime original — s'inspirer du
concept ("fenêtre système" holographique) sans reproduire d'assets protégés.

---

## Système de design (à respecter strictement)

**Couleurs :**
- Fond principal : `#0A0E17` (bleu-noir profond)
- Panneaux/cartes : `#121826` avec bordure `#1E293B`, effet verre dépoli léger
- Accent Système (neutre, UI générale, XP, niveaux) : `#3A86FF` → glow `#4CC9F0`
- Accent Budget (stat OR) : `#F2B705` (ambre)
- Accent Sport (stat FOR/VIT) : `#E63946` (rouge)
- Accent Habitudes (stat INT) : `#2DC653` (vert émeraude)
- Accent Calendrier (stat PER) : `#9D4EDD` (violet)
- Texte principal : `#E6EDF3`, texte secondaire : `#8B98A8`

**Typographie :**
- Titres, chiffres (niveau, XP, stats) : une police display anguleuse type "Rajdhani" —
  caractère HUD/interface futuriste, utilisée avec retenue
- Corps de texte, labels de boutons : une police sans-serif lisible type "Inter"
- Tags système, timestamps, petites étiquettes (`[QUÊTE DU JOUR]`) : une police monospace type
  "JetBrains Mono"

**Layout :** mobile-first (gabarit ~390×844), navigation par onglets en bas (5 icônes : Accueil,
Sport, Budget, Habitudes, Calendrier), cartes à coins nets avec fins liserés lumineux plutôt que
des ombres portées classiques.

**Signature visuelle :** un badge de rang hexagonal (contour lumineux, une lettre de rang E→S
au centre) réutilisé comme motif récurrent, + des coins d'angle façon viseur HUD (petits
crochets ⌐ aux quatre coins) sur les cartes principales — pas de bordures arrondies génériques.

---

## Écrans à concevoir

### 1. Accueil / Rapport Quotidien (écran principal)
- En-tête : nom du Chasseur, badge de rang hexagonal, niveau + barre d'XP circulaire
- Carte "Rapport Quotidien" en haut, ton "voix du Système" : ex. *"[SYSTÈME] Chasseur, 3 quêtes
  vous attendent aujourd'hui. Complétez l'Épreuve du Jour pour progresser en FOR."*
- 5 barres de statistiques (FOR, VIT, INT, OR, PER), chacune avec sa couleur d'accent
- Compteur de série ("Série actuelle : 12 jours")
- Liste des quêtes du jour, toutes catégories confondues

### 2. Fenêtre de Statut (profil détaillé)
- Vue agrandie du badge de rang + progression vers le rang suivant
- Graphique radar des 5 statistiques
- Historique de niveaux / historique de séries

### 3. Onglet Sport
- Carte "Quête du Jour" (titre, description, récompense XP, bouton "Terminer")
- Stats FOR/VIT en évidence
- Historique récent des quêtes complétées

### 4. Onglet Budget
- Barre de saisie rapide en langage naturel ("15€ déjeuner") avec icône micro/clavier
- Progression du mois par catégorie (barres avec limite)
- Stat OR en évidence, objectif d'épargne actif avec barre de progression
- Liste des transactions récentes
- Mention discrète en bas : *"Ceci n'est pas un conseil financier."*

### 5. Onglet Habitudes & Tâches
- Liste d'habitudes avec indicateur de série (icône flamme + nombre de jours)
- Liste de tâches du jour avec cases à cocher, récompense XP visible
- Bouton flottant "+" pour ajouter une habitude ou tâche

### 6. Onglet Calendrier
- Vue journée avec timeline des événements
- Créneau de quête suggéré par le Système mis en évidence visuellement (distinct des
  événements normaux)

### 7. Écran Onboarding / "Éveil"
- Questionnaire court (objectifs : sport / budget / habitudes / tout)
- Écran de révélation dramatique du profil Chasseur généré (rang E de départ, stats initiales)

### 8. Coach (chat avec le Système)
- Interface de chat simple, bulles côté Système avec le style visuel de la marque, ton direct
  et motivant sans être moralisateur

---

## Ton de la rédaction (copy)
- Voix du Système : phrases courtes, présent, parfois entre crochets `[...]`, jamais culpabilisant
  en cas de quête manquée
- Boutons : verbes d'action précis — "Terminer", "Passer", "Valider", "Ajouter" (pas "Soumettre")
- Écrans vides : formulés comme une invitation à agir, pas comme un manque

---

## Livrable attendu
Un set d'écrans mobiles haute-fidélité pour les 8 écrans ci-dessus, cohérents visuellement,
utilisables ensuite comme référence pour l'implémentation Flutter.
