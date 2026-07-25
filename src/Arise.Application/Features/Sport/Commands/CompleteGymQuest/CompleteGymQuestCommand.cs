using Arise.Application.Common.Messaging;

namespace Arise.Application.Features.Sport.Commands.CompleteGymQuest;

/// <summary>
/// Le Chasseur déclare avoir accompli sa quête de sport.
///
/// <para>Le fuseau voyage dans la commande, comme dans la demande de quête du jour et pour la
/// même raison : c'est lui qui décide du jour que la série comptera. Il disparaîtra des deux le
/// jour où <c>HunterProfile</c> le portera.</para>
///
/// <para><paramref name="HunterProfileId"/> est annoncé par l'appelant tant que le rattachement
/// au jeton d'authentification n'est pas posé — le handler vérifie néanmoins que la quête lui
/// appartient, sans quoi n'importe quel Chasseur complèterait celles des autres.</para>
/// </summary>
public sealed record CompleteGymQuestCommand(
    Guid HunterProfileId,
    Guid QuestId,
    string FuseauHoraire) : ICommand<CompleteGymQuestResult>;

/// <summary>
/// Ce que la complétion rend à l'écran : le strict nécessaire pour confirmer l'accomplissement
/// et annoncer le gain.
/// </summary>
/// <param name="CompletedAt">
/// L'instant de l'accomplissement, <b>en UTC</b> : c'est la seule part de cette date qui
/// survive à l'aller-retour en base, et le client la rend dans le fuseau du Chasseur — celui-là
/// même qu'il vient d'envoyer dans la commande. Le rendre décalé donnerait deux sources de
/// vérité pour la même chose.
/// </param>
/// <param name="DejaCompletee">
/// <see langword="true"/> quand la quête était déjà accomplie avant cet appel — double-tap sur
/// le bouton, renvoi réseau du client, deux appareils. Ce n'est pas une erreur :
/// l'accomplissement tient, et c'est ce drapeau — pas le montant — qui dit que le gain était
/// déjà au compteur.
/// </param>
/// <param name="XpAcquis">
/// L'XP que cette quête vaut au Chasseur, qu'il vienne de lui être accordé ou qu'il l'ait déjà
/// été. Rendre 0 sur un second tap afficherait « +0 XP » à quelqu'un qui a bel et bien fait sa
/// séance ; avec ce montant et <paramref name="DejaCompletee"/>, le client peut écrire
/// « Déjà accomplie — 25 XP acquis ».
/// </param>
public sealed record CompleteGymQuestResult(
    Guid QuestId,
    DateTimeOffset CompletedAt,
    bool DejaCompletee,
    int XpAcquis);
