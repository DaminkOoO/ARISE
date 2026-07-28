using Arise.Application.Common.Messaging;

namespace Arise.Application.Features.Tasks.Queries.GetTasks;

/// <summary>
/// Ce qu'il reste à faire au Chasseur.
/// </summary>
public sealed record GetTasksQuery(Guid HunterProfileId) : IQuery<IReadOnlyList<TaskSummary>>;

/// <summary>
/// Une ligne de la liste. Pas de drapeau « faite » : la requête ne rend que ce qui reste, et un
/// drapeau toujours <see langword="false"/> inviterait l'écran à filtrer une seconde fois.
/// </summary>
public sealed record TaskSummary(Guid TaskId, string Title, DateOnly? DueDate);
