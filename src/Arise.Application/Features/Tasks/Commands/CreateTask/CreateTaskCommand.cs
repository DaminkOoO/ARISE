using Arise.Application.Common.Messaging;

namespace Arise.Application.Features.Tasks.Commands.CreateTask;

/// <summary>
/// Le Chasseur se donne quelque chose à faire.
///
/// <para>Pas de fuseau horaire, contrairement aux commandes d'habitude et de quête : rien ici ne
/// dépend du <b>jour</b> du Chasseur. L'échéance est une date qu'il choisit lui-même, et la date
/// de création n'est qu'un départage d'affichage.</para>
/// </summary>
/// <param name="DueDate">
/// Échéance facultative. Une date passée est acceptée : le Chasseur qui note « Rappeler la
/// banque » pour vendredi dernier sait qu'il est en retard, et le lui refuser l'empêcherait
/// d'écrire ce qu'il a à faire.
/// </param>
public sealed record CreateTaskCommand(
    Guid HunterProfileId,
    string Title,
    DateOnly? DueDate) : ICommand<CreateTaskResult>;

/// <summary>
/// Ce que la déclaration rend à l'écran : de quoi afficher la tâche neuve et enchaîner sur sa
/// complétion sans relire toute la liste.
/// </summary>
public sealed record CreateTaskResult(Guid TaskId, string Title, DateOnly? DueDate);
