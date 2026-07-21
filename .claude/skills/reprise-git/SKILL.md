---
name: reprise-git
description: Rattraper une erreur git sur ARISE en choisissant la manœuvre la moins destructive — annuler un commit, désindexer, corriger un message, récupérer du travail perdu, sortir d'une mauvaise branche, ou retirer un secret de l'historique. Utilise cette skill dès que l'utilisateur dit "j'ai commité par erreur", "annule", "reviens en arrière", "j'ai perdu mon travail", "mauvaise branche", "undo", "revert", "j'ai poussé une clé" — en français comme en anglais. Consulte-la AVANT toute commande git destructive (reset --hard, checkout --, rebase, filter-branch, push --force).
---

# Rattraper une erreur git sur ARISE

Deux faits sur ce dépôt changent tout par rapport au cas général : **il n'y a pas de remote,
et rien n'a jamais été poussé**. Réécrire l'historique n'expose donc personne d'autre et ne
casse aucun clone. Les manœuvres ci-dessous sont sûres tant que c'est vrai — **dès qu'un
remote est ajouté, réévalue** : ce qui est publié ne se rattrape plus discrètement.

## La règle de base

Presque rien n'est réellement perdu en git : les commits restent atteignables via
`git reflog` pendant des semaines. Ce qui se perd vraiment, c'est le travail **non committé**
écrasé par un `reset --hard` ou un `checkout --`.

Donc, avant toute commande destructive : `git stash -u` ou `git status` pour savoir ce qui
serait détruit. Prendre dix secondes ici évite de reconstituer une heure de travail.

Et confirme avec l'utilisateur avant d'exécuter une commande destructive — c'est son travail,
pas le tien.

## Quelle manœuvre pour quel problème

**Le dernier commit a un mauvais message** (rien n'est poussé)
`git commit --amend` — réécrit le message sans toucher au contenu.

**Le dernier commit a oublié un fichier**
`git add <fichier>` puis `git commit --amend --no-edit`.

**Le dernier commit est à jeter, mais garder le travail**
`git reset --soft HEAD~1` — le commit disparaît, les modifications restent indexées.

**Le dernier commit est à jeter, travail compris**
`git reset --hard HEAD~1` — destructif. Vérifie `git status` d'abord.

**Un fichier a été indexé par erreur**
`git restore --staged <fichier>` — le désindexe sans perdre les modifications.

**Des artefacts de build ont été commités** (`bin/`, `obj/`, `.dart_tool/`)
Corrige d'abord le `.gitignore`, puis `git rm -r --cached <dossier>` et commite. Le
`.gitignore` seul ne désuit pas ce qui est déjà suivi — c'est la confusion la plus fréquente.

**On travaille sur la mauvaise branche, rien n'est committé**
`git stash -u`, `git switch <bonne-branche>`, `git stash pop`.

**Du travail semble perdu après une manœuvre**
`git reflog` — retrouve le SHA de l'état voulu, puis `git switch -c recuperation <sha>`.
Crée une branche plutôt que de sauter dessus : ça ne détruit rien si c'est le mauvais SHA.

## Un secret est entré dans l'historique

L'ordre compte, et il n'est pas intuitif :

1. **Révoque la clé d'abord.** Tant que la clé est valide, elle est compromise — le nettoyage
   de l'historique ne change rien à ça. Régénère la clé Gemini côté fournisseur.
2. **Puis nettoie l'historique.** Si c'est le dernier commit et que rien n'est poussé :
   `git reset --soft HEAD~1`, retire le secret, recommite. Si le commit est plus ancien, la
   réécriture est plus lourde (`git filter-repo`) — préviens l'utilisateur du coût avant de
   commencer.
3. **Ajoute le fichier au `.gitignore`** pour que ça ne recommence pas.

Ne saute jamais l'étape 1 en te disant que l'historique local n'a jamais quitté la machine :
le coût d'une rotation de clé est de cinq minutes, celui d'une fuite est ouvert.

## Ce qu'il ne faut pas faire

- `git push --force` sur une branche partagée. Sans objet ici (pas de remote), mais la
  question se reposera.
- `git checkout .` ou `git reset --hard` sans avoir regardé `git status` — c'est la seule
  façon courante de perdre du travail définitivement.
- Enchaîner des commandes de rattrapage en espérant que ça retombe sur ses pieds. Si la
  situation n'est pas claire, `git status` + `git log --oneline -10` + `git reflog -10`, on
  comprend, puis on agit une fois.
