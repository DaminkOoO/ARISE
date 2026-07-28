using Arise.Application.Common.Messaging;

namespace Arise.Application.Features.Tasks.Commands.CompleteTask;

/// <summary>
/// Le Chasseur coche une tâche.
///
/// <para><paramref name="FuseauHoraire"/> ne sert <b>pas</b> à dater la complétion — celle-ci
/// est un instant absolu, et aucune fenêtre ne borne le droit de cocher une tâche en retard.
/// Il sert au <b>plafond quotidien d'XP d'engagement</b> (doc mécaniques, section 1), qui se
/// recompte sur la journée du Chasseur et non sur celle du serveur. Sans XP à la clé, cette
/// commande n'en aurait aucun besoin.</para>
///
/// <para><paramref name="HunterProfileId"/> est annoncé par l'appelant tant que le rattachement
/// au jeton d'authentification n'est pas posé — le handler vérifie néanmoins que la tâche lui
/// appartient, sans quoi n'importe quel Chasseur cocherait celles des autres.</para>
/// </summary>
public sealed record CompleteTaskCommand(
    Guid HunterProfileId,
    Guid TaskId,
    string FuseauHoraire) : ICommand<CompleteTaskResult>;

/// <summary>
/// Ce que la complétion rend à l'écran.
/// </summary>
/// <param name="CompletedAt">
/// L'instant de la complétion, <b>en UTC</b> : c'est la seule part de cette date qui survive à
/// l'aller-retour en base, et le client la rend dans le fuseau du Chasseur. Pour une tâche déjà
/// faite, c'est l'instant de la <b>première</b> complétion — afficher celui du second tap
/// réécrirait l'histoire pour un geste qui n'a rien changé.
/// </param>
/// <param name="DejaCompletee">
/// <see langword="true"/> quand la tâche était déjà faite avant cet appel — double-tap, renvoi
/// réseau, deux appareils. Ce n'est pas une erreur : la tâche est faite.
/// </param>
/// <param name="XpAcquis">
/// L'XP accordé par ce geste. Vaut 0 quand la tâche était déjà faite — le gain a été accordé au
/// premier appel — ou quand le plafond quotidien d'engagement est atteint. Dans ce dernier cas
/// la tâche est bel et bien cochée : c'est le gain qui est rogné, jamais le geste.
/// </param>
public sealed record CompleteTaskResult(
    Guid TaskId,
    DateTimeOffset CompletedAt,
    bool DejaCompletee,
    int XpAcquis);
