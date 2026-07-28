using Arise.Domain.Tasks;

namespace Arise.Application.Common.Abstractions;

/// <summary>
/// Accès aux tâches ponctuelles des Chasseurs. Implémenté dans la couche Infrastructure : la
/// couche Application ignore qu'il y a un PostgreSQL derrière.
/// </summary>
public interface ITaskItemRepository
{
    /// <summary>
    /// La tâche portant cet identifiant, ou <see langword="null"/>. Rend la tâche <b>faite
    /// comprise</b> : c'est à l'appelant de constater qu'elle l'était déjà, ce qui n'est pas une
    /// erreur mais un double-tap.
    /// </summary>
    Task<TaskItem?> GetByIdAsync(Guid taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Toutes les tâches de ce Chasseur, faites comprises. Le tri sur celles à afficher est la
    /// décision de la requête de lecture, pas du stockage — même partage que
    /// <see cref="IHabitRepository.GetForHunterAsync"/> avec les archivées.
    /// </summary>
    Task<IReadOnlyList<TaskItem>> GetForHunterAsync(
        Guid hunterProfileId, CancellationToken cancellationToken);

    Task AddAsync(TaskItem task, CancellationToken cancellationToken);

    Task SaveAsync(TaskItem task, CancellationToken cancellationToken);
}
