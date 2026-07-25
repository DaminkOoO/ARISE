using MediatR;

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
    string FuseauHoraire) : IRequest<CompleteGymQuestResult>;

/// <summary>
/// Ce que la complétion rend à l'écran : le strict nécessaire pour confirmer l'accomplissement
/// et annoncer le gain.
/// </summary>
/// <param name="DejaCompletee">
/// <see langword="true"/> quand la quête était déjà accomplie avant cet appel — double-tap sur
/// le bouton, renvoi réseau du client. Ce n'est pas une erreur : l'accomplissement tient, et
/// <paramref name="XpGagne"/> vaut alors 0 parce que l'XP a déjà été accordé, pas parce qu'il
/// est refusé.
/// </param>
public sealed record CompleteGymQuestResult(
    Guid QuestId,
    DateTimeOffset CompletedAt,
    bool DejaCompletee,
    int XpGagne);
