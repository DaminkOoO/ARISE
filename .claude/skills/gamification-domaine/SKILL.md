---
name: gamification-domaine
description: Le moteur central d'ARISE — XP, niveau, rang et séries (streaks) du Chasseur — et la discipline pour le modéliser sans le disséminer. Utilise cette skill dès qu'une tâche touche HunterProfile, l'attribution d'XP (AwardXp), un passage de niveau ou de rang, un événement de domaine (HunterRankedUpEvent), une série de jours, ou tout calcul de progression. Ce moteur est réutilisé par les sept phases : une formule dupliquée ou une série mal datée s'y propage partout.
---

# Le moteur XP / niveau / rang / séries

C'est la fondation. La Phase 1 le pose, les six suivantes s'y branchent (sport, budget,
habitudes, calendrier alimentent tous le même compteur d'XP). Une erreur ici n'est pas locale :
elle calcule le mauvais niveau de Chasseur dans toute l'app, et aucun garde-fou produit ne
l'attrape — seul [[revue-code]] le fait. D'où cette skill.

## La formule est une source de vérité unique — jamais dupliquée

Le passage XP → niveau, et niveau → rang, est une **fonction pure** définie à **un seul
endroit** (un barème dans le domaine), et rien d'autre ne recalcule ces seuils. Un handler qui
« sait » qu'on passe niveau 5 à 500 XP est un second exemplaire de la règle : le jour où le
barème change, l'un des deux est oublié. Les handlers *appellent* le barème, ils ne le
réimplémentent pas.

**Tu ne connais pas les chiffres de mémoire.** La courbe d'XP, l'échelle des rangs (E, D, C,
B, A, S… selon la spec) et leurs seuils viennent du document de référence, pas de ton
intuition « façon Solo Leveling ». S'ils ne sont pas dans le dépôt ni fournis, **arrête-toi et
demande-les** — ne pose pas une courbe inventée qu'il faudra défaire quand la vraie arrivera,
en cassant tous les profils déjà calculés. C'est exactement le cas où deviner coûte plus cher
que d'attendre.

## Modélise en types du domaine, pas en `int` nus

XP, Niveau et Rang portent des invariants — ne les laisse pas être des `int` et `string`
promenés partout. Un value object (ou un `record` avec garde) refuse un XP négatif à la
construction, et non trois lignes plus loin quand quelqu'un pense à vérifier. L'XP est
**monotone** : on en gagne, on n'en retire pas silencieusement.

Le niveau **dérive** de l'XP (il ne se stocke pas comme une vérité indépendante qui pourrait
diverger) ; le rang dérive du niveau. Une seule valeur fait foi — l'XP total — et le reste se
recalcule.

## Le passage de rang émet un événement, il ne branche pas en ligne

Quand un gain d'XP franchit un seuil de rang, le domaine lève un `HunterRankedUpEvent` ;
`AwardXpCommand` le publie via MediatR, et ce qui doit réagir (notification, rapport,
déblocage) s'y abonne. Pas de `if (nouveauRang != ancienRang) { … }` qui fait tout dans le
handler d'XP : le jour où le sport ET le budget accordent de l'XP, la logique de rang doit
vivre à un seul endroit. Voir [[tdd-cqrs]] : une commande en déclenche une autre par MediatR
ou par événement, jamais par appel direct.

## Les séries (streaks) sont un champ de mines de dates

Une série compte des **jours de calendrier consécutifs**, et « jour » dépend d'un fuseau — pas
d'`UtcNow` brut. Décide et teste explicitement :

- **Quel fuseau** délimite la journée du Chasseur ? Deux actions à 23 h et 1 h peuvent tomber
  le même jour local et des jours UTC différents — ou l'inverse.
- **Une série se rompt** après un jour sans action, pas après 24 h glissantes. Teste la
  frontière : dernière action hier 23 h, nouvelle action aujourd'hui 00 h 05 → la série tient.
- **Idempotence** : deux actions le même jour n'incrémentent pas la série deux fois.
- **Changement d'heure (DST)** : une journée peut faire 23 ou 25 h. Un test qui traverse un
  passage à l'heure d'été doit rester vert.

Injecte l'horloge (`TimeProvider` / une abstraction), ne lis jamais `DateTime.Now` dans le
domaine : une série ne se teste pas si le temps n'est pas contrôlable.

## Le ton d'une série rompue n'est jamais culpabilisant

Une série cassée est un fait, pas une faute. Le calcul est ici ; la **formulation** relève de
[[garde-fous]] (règle du ton), mais rappelle-le dès la modélisation : ne nomme pas une méthode
`PunishMissedDay`, ne stocke pas un « échec ». On repart, on ne sanctionne pas.

## Ce que les tests doivent couvrir

- Les **frontières** de seuil : `seuil − 1 XP` reste au niveau N, `seuil` bascule à N+1.
- La **frontière de rang** : le dernier niveau d'un rang, le premier du suivant.
- L'événement `HunterRankedUpEvent` est levé **une fois**, au bon franchissement, jamais sur
  un gain intra-rang.
- Les séries : rupture, reprise, même jour deux fois, minuit local, DST.

Table-driven de préférence — une ligne par cas de frontière — pour que l'ajout d'un palier
soit une ligne de données, pas un nouveau test copié-collé.
