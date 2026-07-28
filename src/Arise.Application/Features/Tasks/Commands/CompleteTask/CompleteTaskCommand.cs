using Arise.Application.Common.Messaging;

namespace Arise.Application.Features.Tasks.Commands.CompleteTask;

/// <summary>
/// Le Chasseur coche une tâche.
///
/// <para>Pas de fuseau horaire, contrairement à <c>CompleteGymQuestCommand</c> : rien ici ne
/// dépend du jour du Chasseur. Il n'y a ni série à créditer — c'est l'affaire des quêtes — ni
/// fenêtre de complétion à faire respecter : une tâche en retard reste à faire, et la cocher
/// trois semaines après reste la bonne chose à faire.</para>
///
/// <para><paramref name="HunterProfileId"/> est annoncé par l'appelant tant que le rattachement
/// au jeton d'authentification n'est pas posé — le handler vérifie néanmoins que la tâche lui
/// appartient, sans quoi n'importe quel Chasseur cocherait celles des autres.</para>
/// </summary>
public sealed record CompleteTaskCommand(
    Guid HunterProfileId,
    Guid TaskId) : ICommand<CompleteTaskResult>;

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
public sealed record CompleteTaskResult(
    Guid TaskId,
    DateTimeOffset CompletedAt,
    bool DejaCompletee);
