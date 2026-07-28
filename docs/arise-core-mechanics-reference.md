# ARISE — Mécaniques du jeu : XP, rang, séries, quêtes, onboarding

Référence de conception pour la Phase 1. Tant que ces chiffres ne sont pas fixés ici, ne les
invente pas ailleurs dans le code — ce document est la source de vérité.

---

## 1. XP, niveaux et rangs

### Formule de progression
```
XpToNextLevel(niveau) = 100 + (niveau - 1) * 20
```
- Niveau 1 → 2 : 100 XP
- Niveau 2 → 3 : 120 XP
- Niveau 3 → 4 : 140 XP
- ...
- Niveau 24 → 25 : 560 XP

Linéaire et simple à tester unitairement — pas de courbe exponentielle qui complique les
tests et le débogage pour un gain d'équilibrage marginal sur une app personnelle.

### Seuils de rang (par niveau)
| Rang | Niveaux |
|---|---|
| E | 1–4 |
| D | 5–9 |
| C | 10–14 |
| B | 15–19 |
| A | 20–24 |
| S | 25+ |

Rang E délibérément plus court (4 niveaux au lieu de 5) — premier rang-up rapide pour
accrocher l'utilisateur tôt, puis rythme plus long ensuite. Rang S n'a pas de plafond ; le
niveau continue de monter indéfiniment au-delà de 25.

**Rythme attendu :** avec ~60–100 XP/jour en usage réel (3-4 quêtes quotidiennes), le rang S
est atteint en ~3 mois d'usage régulier. Cumul total pour atteindre le niveau 25 : 7920 XP.

### Logique de niveau/rang (`HunterProfile.AwardXp`)
```
AwardXp(montant):
    CurrentXp += montant
    tant que CurrentXp >= XpToNextLevel(Level):
        CurrentXp -= XpToNextLevel(Level)
        Level += 1
        si RankFor(Level) != Rank:
            Rank = RankFor(Level)
            publier HunterRankedUpEvent
    XpToNextLevel = XpToNextLevel(Level)  # recalculé pour le niveau courant
```
Boucle `tant que` (pas juste `si`) — un seul gros gain d'XP (ex. complétion de Boss Raid) peut
faire monter plusieurs niveaux d'un coup, il faut le gérer correctement.

### Récompenses XP par qualificatif de difficulté
Bornes de validation à appliquer côté agent (jamais faire confiance à ce que Gemini renvoie
sans vérifier que `xp_reward` tombe dans la bonne fourchette) :

| Difficulté | XP |
|---|---|
| easy | 10–15 |
| medium | 15–25 |
| hard | 25–40 |
| Quête de pénalité | 10 (fixe, toujours facile par conception) |
| Boss Raid (hebdomadaire) | 100–200 |

### XP d'engagement — habitudes et tâches

Les quêtes ne sont pas le seul chemin de progression : tenir une habitude et cocher une tâche
rapportent aussi. Ces montants sont **fixes** — ils ne sortent d'aucun agent, et rien ne les
valide côté LLM.

| Geste | XP |
|---|---|
| Habitude **quotidienne** tenue | 3 |
| Habitude **hebdomadaire** tenue | 10 |
| Tâche complétée | 5 |
| **Plafond quotidien cumulé (habitudes + tâches)** | **25** |

**Pourquoi si peu, et pourquoi un plafond.** Le rythme annoncé plus haut — rang S en ~3 mois,
7920 XP, soit ~88 XP/jour — est calculé sur les seules quêtes. Tout ce qu'on ajoute à côté
raccourcit ce délai. Les montants ci-dessus sont donc dimensionnés pour rester une garniture :

- Usage réaliste (2 habitudes + 1 tâche par jour) : +11 XP/jour, soit 99 au total → niveau 25
  en **80 jours**. Le rythme du document tient.
- Usage maximal (plafond atteint tous les jours) : +25 XP/jour, soit 113 → niveau 25 en
  **70 jours**. Un Chasseur qui tient tout, tous les jours, mérite d'arriver plus tôt.

**Le plafond est la seule protection contre la ferme d'XP**, et il est indispensable : le
nombre de quêtes du jour est fixé par le Système, mais le nombre d'habitudes et de tâches est
fixé par le Chasseur lui-même. Sans plafond, déclarer cinquante tâches et les cocher d'affilée
vaut 250 XP en une minute. C'est pourquoi le plafond est **cumulé** entre les deux domaines
plutôt que séparé par domaine : deux plafonds séparés se contournent en alternant.

**Une hebdomadaire vaut plus qu'une quotidienne (10 contre 3)** parce qu'elle est un engagement
unique et non sept petits : la payer 3 comme une quotidienne rendrait le rythme hebdomadaire
strictement perdant, et personne ne le choisirait.

**Le jour de référence est celui du Chasseur**, comme partout ailleurs (section 2) : le plafond
se recompte sur les gestes de sa journée à lui, pas sur celle du serveur.

**Rien n'est stocké.** L'XP déjà acquis dans la journée se **recalcule** depuis les gestes du
jour — journal des habitudes et tâches cochées — exactement comme la série d'une habitude se
recalcule depuis son journal. Un compteur entretenu à côté divergerait à la première écriture
concurrente.

---

## 2. Séries (streaks)

**Ce qui compte pour la série du Chasseur (`HunterProfile.StreakCurrent`) :** compléter au
moins une Quête de type `daily` ou `penalty`, **de n'importe quel domaine**, un jour donné
(fuseau horaire du Chasseur). C'est délibérément large — pas besoin de compléter TOUTES les
quêtes du jour, juste une seule.

**Important — à ne pas confondre :** ceci est distinct des séries par habitude individuelle
(`Habit` a sa propre série calculée depuis `HabitLog`). La série du profil Chasseur est un
indicateur d'engagement global ; les séries d'habitudes sont locales à chaque habitude.

### Champs sur `HunterProfile`
```
StreakCurrent: int
StreakLongest: int
LastCompletionDate: DateOnly?
```

### Enregistrement d'une complétion (`HunterProfile.RegisterDailyCompletion(today)`)
Appelé par un handler central (`StreakUpdateHandler`) qui écoute un événement de domaine
`QuestCompletedEvent` — peu importe quel handler de commande (Sport, Budget, etc.) a déclenché
la complétion, la logique de série reste centralisée à un seul endroit.

```
RegisterDailyCompletion(today):
    si LastCompletionDate != null et today <= LastCompletionDate:
        ne rien faire  # déjà comptabilisé ce jour-là, ou jour révolu — voir ci-dessous
    sinon si LastCompletionDate == today - 1 jour:
        StreakCurrent += 1
    sinon:
        StreakCurrent = 1  # première complétion, ou trou de ≥2 jours → repart de 1
    LastCompletionDate = today
    StreakLongest = max(StreakLongest, StreakCurrent)
```

**La série ne recule jamais.** Le jour du Chasseur peut reculer sans qu'il ait rien fait
d'anormal : deux appareils réglés sur des fuseaux différents (tablette restée à Paris, où il
est déjà le 26 ; téléphone passé à New York, où il est encore le 25), un vol vers l'ouest, un
changement manuel de fuseau. Sans la garde `today <= LastCompletionDate`, cette date antérieure
tombe dans la branche « trou de ≥2 jours » et fait retomber à 1 une série de cinq jours — une
perte que le Chasseur n'a pas méritée. Le prix accepté en échange : une complétion tardive
rattrapant un jour déjà dépassé n'ajoute pas de maillon rétroactif. `StreakCurrent` est un
compteur d'engagement, pas un journal ; recalculer la série depuis un historique de complétions
demanderait ce journal, qui n'existe pas.

Budget, Habitudes et Calendrier appellent cette même méthode : la garde vaut pour eux tous.

### Quel jour la complétion crédite-t-elle ? Celui de la quête, jamais celui du tap

`QuestCompletedEvent.JourDuChasseur` vaut **`Quest.QuestDate`** — le jour pour lequel la quête a
été posée, celui dont le Chasseur a lu le texte le matin. Pas le jour du tap, pas celui du
serveur.

Le cas qui tranche : la séance a lieu le 25 à 23h50, le Chasseur tape « Terminé » à 00h05 le 26.
Dater la série sur le tap créditerait le 26 et laisserait le 25 vide — série rompue pour
quelqu'un qui n'a rien manqué. L'effort appartient au jour de la quête. Corollaire utile :
aucun appelant (handler, worker, import) ne peut plus déformer cette date en passant une horloge
de serveur, puisqu'elle n'est plus déduite d'un instant mais lue sur l'entité.

### Fenêtre de complétion : le jour de la quête, ou la veille

Une quête ne se complète que si `QuestDate >= aujourd'hui (fuseau du Chasseur) - 1 jour`. Plus
ancienne, la complétion est refusée.

Sans cette borne, un Chasseur revenu après dix jours d'absence complèterait les dix quêtes
laissées derrière lui — 10 × 20 XP en une minute — et la progression cesserait de mesurer quoi
que ce soit. Un jour de battement, et pas zéro, parce que le tap arrive parfois après minuit et
qu'un décalage de fuseau suffit à faire tourner la date sans que le Chasseur soit en retard.

Refus, et non dévaluation : une quête à demi créditée demanderait au Chasseur de comprendre
pourquoi son gain a fondu. Le Système constate que le jour est révolu et pose la quête suivante.

### Rupture de série (`HunterProfile.CheckStreakBreak(today)`)
Appelé une fois par jour par le `briefing-worker`, **avant** la génération des quêtes du jour.
```
CheckStreakBreak(today):
    si LastCompletionDate != null
       et LastCompletionDate < today - 1 jour
       et StreakCurrent > 0:
        StreakCurrent = 0
        retourner StreakJustBroken = true   # signal pour générer une quête de pénalité
    retourner StreakJustBroken = false
```

Ces deux méthodes sont des fonctions pures (date + état → nouvel état) — cas idéal pour les
tests `Arise.Domain.Tests` en TDD, aucun mock nécessaire.

---

## 3. Modèle de quête générée

### Contrat de sortie de `IQuestGenerationAgent` (et agents similaires par domaine)
Schéma JSON strict (mode schema Gemini) :
```json
{
  "title": "string, court, en français",
  "description": "string, 1-2 phrases, voix Système",
  "type": "daily | penalty",
  "stat_target": "FOR | VIT | INT | OR | PER",
  "difficulty": "easy | medium | hard",
  "xp_reward": "integer"
}
```
Validation obligatoire côté C# avant sauvegarde : `xp_reward` dans la fourchette de la
`difficulty` déclarée (Section 1), `stat_target` et `type` dans l'enum attendu. En cas
d'échec de validation : un seul retry avec rappel strict, puis repli sur une quête générique
sûre plutôt que de faire échouer la requête utilisateur.

### Exemple concret (domaine Sport, correspond à la maquette déjà validée)
```json
{
  "title": "L'Épreuve du Guerrier",
  "description": "40 pompes, 3 séries de squats, 5 minutes de gainage.",
  "type": "daily",
  "stat_target": "FOR",
  "difficulty": "medium",
  "xp_reward": 20
}
```

### Contexte fourni à l'agent à chaque génération
- Niveau, rang, stats actuelles du Chasseur
- Objectifs déclarés + niveau de forme physique (pour le domaine Sport)
- 7 derniers jours d'historique : quêtes complétées vs. passées, difficulté ressentie
- Série actuelle

**Difficulté adaptative :** taux de complétion sur 7 jours > 85% → orienter légèrement plus
difficile ; < 40% → orienter plus facile/plus court. Même principe que l'ajustement de
difficulté déjà utilisé dans Mono Tiles, appliqué ici à la charge de quêtes plutôt qu'à la
vitesse des tuiles.

---

## 4. Flux d'onboarding / Éveil

Séquence d'écrans (déjà spécifiée dans le doc de design, résumé ici pour le contexte code) :

1. **Inscription** (00) — nom d'utilisateur + mot de passe
2. **Profil du Chasseur** (01) — pseudo, choix d'emblème, fuseau horaire
3. **Objectifs** (02) — Sport / Budget / Habitudes / Calendrier / Tout
4. **Configuration rapide par domaine** (03a-d) — uniquement les écrans correspondant aux
   objectifs choisis
5. **Notifications** (04) — horaires de rapport quotidien / rappel / alerte de série
6. **Récapitulatif** (05)
7. **Éveil** (07b) — écran de révélation

### Décision de conception importante : les valeurs de départ sont fixes, pas générées par l'IA
Contrairement à ce qu'on pourrait supposer, **le niveau, le rang et les 5 stats de départ ne
sont jamais générés par Gemini** — ce sont des constantes déterministes :
```
Level = 1
Rank = E
Stats = { FOR: 10, VIT: 10, INT: 10, GOLD: 10, PER: 10 }
```
L'équilibrage du jeu ne doit pas dépendre du caprice d'un LLM, et ça reste testable
trivialement. Le seul rôle de `IOnboardingAgent` est de générer le **texte narratif**
personnalisé de l'écran Éveil (le message "[SYSTÈME] L'ÉVEIL A COMMENCÉ...") à partir des
objectifs déclarés — jamais les chiffres.

### Contrat de sortie de `IOnboardingAgent`
```json
{
  "awakening_narrative": "string, voix Système, 1-3 phrases, personnalisé selon les objectifs déclarés"
}
```
`OnboardHunterCommandHandler` combine ce texte avec les valeurs fixes ci-dessus pour créer le
`HunterProfile` — l'agent ne touche jamais aux champs numériques du profil créé.

---

## Pour le design (déjà livré, non reproduit ici)

Le brief de design (couleurs, typographie Rajdhani/Inter/JetBrains Mono, coins HUD, lueurs) et
le prompt de correction ont déjà été générés plus tôt dans ce projet — mêmes fichiers,
toujours valables, pas de changement nécessaire pour la Phase 1.
