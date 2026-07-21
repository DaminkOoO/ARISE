---
name: garde-fous
description: Vérifie que le code respecte les garde-fous produit non négociables d'ARISE — budget (aucun conseil financier), sport (aucune prescription chiffrée ni diagnostic), ton jamais culpabilisant, prix de courses horodatés et jamais « temps réel », et interface intégralement en français. Utilise cette skill avant de clôturer une tâche touchant au budget, au sport, au coach, aux quêtes, aux courses, à un prompt Gemini ou à du texte affiché à l'utilisateur, et chaque fois qu'une revue est demandée. Ces règles engagent la sécurité de l'utilisateur, pas seulement la qualité du code.
---

# Revue des garde-fous produit

ARISE donne des conseils sur l'argent, le corps et les habitudes de quelqu'un. Ces règles
existent parce qu'une sortie mal cadrée peut causer un vrai préjudice — pas parce qu'elles
rendent le code plus propre.

## Le point central : prompt ≠ garde-fou

Une consigne écrite dans un prompt Gemini est une **suggestion** au modèle. Elle se contourne
à la première réponse inattendue. Un garde-fou est du C# qui rejette la sortie non conforme.

À chaque règle ci-dessous, la question à poser est : *si le modèle ignorait complètement le
prompt, qu'est-ce qui, dans le code, empêcherait cette sortie d'atteindre l'écran ?* Si la
réponse est « rien », le garde-fou est manquant, même si le prompt est irréprochable.

## Checklist

**Budget**
- Aucun conseil d'investissement, de dette ou de fiscalité — même formulé prudemment, même en
  réponse à une question directe de l'utilisateur. Uniquement des nudges comportementaux.
- Les catégories de dépenses sont bornées par une liste autorisée, validée en code. Une
  catégorie libre venant du modèle ou de l'utilisateur est refusée, pas normalisée.

**Sport et coach RAG**
- Aucune prescription numérique précise : ni charges, ni séries×répétitions chiffrées, ni
  allures, ni calories, ni durées présentées comme une consigne médicale.
- Aucun diagnostic de blessure, aucune interprétation de symptôme.
- Toute mention de douleur par l'utilisateur renvoie vers un professionnel de santé. Vérifie
  que ce chemin existe en code et qu'il est testé.

**Ton**
- Une quête manquée, une série rompue, un objectif raté ne sont jamais présentés de façon
  culpabilisante. Relis la copie française à voix haute : si une phrase ferait culpabiliser
  quelqu'un un mauvais jour, réécris-la.
- Le registre « Système » de Solo Leveling reste un habillage : il peut être solennel, jamais
  punitif.

**Courses**
- Le scraper reste un outil hors ligne, jamais dans le chemin de requête live de l'API.
- Les prix sont des **relevés horodatés**. L'horodatage est affiché, et aucun libellé ne
  suggère « temps réel », « prix actuel » ou « en direct ».

**Français**
- Tout texte visible par l'utilisateur est en français : libellés, messages d'erreur, erreurs
  de validation, textes de repli des agents, notifications. Les messages de validation
  remontent jusqu'à l'écran — ils comptent.

## Rapport

Passe en revue le diff (`git diff`, ou les fichiers de la tâche en cours) et rends :

```
## Garde-fous — <périmètre revu>

### Violations
- <fichier:ligne> — <règle enfreinte> — <pourquoi c'est un risque> — <correction proposée>

### Points d'attention
- <ce qui tient aujourd'hui mais casserait au prochain changement>

### Vérifié sans remarque
- <règles réellement contrôlées et conformes>
```

Ne liste pas comme « vérifié » une règle sans objet dans le diff — dis qu'elle n'est pas
concernée. Un rapport qui gonfle la colonne verte ne sert personne.
