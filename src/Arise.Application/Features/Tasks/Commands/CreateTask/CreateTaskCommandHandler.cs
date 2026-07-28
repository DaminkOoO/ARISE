using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Domain.Tasks;
using MediatR;

namespace Arise.Application.Features.Tasks.Commands.CreateTask;

/// <summary>
/// Déclare une tâche et rien de plus : aucun XP accordé, aucune série touchée. Se donner quelque
/// chose à faire ne fait pas progresser le Chasseur.
///
/// <para>Aucun contrôle d'unicité du titre, contrairement à <c>CreateHabitCommand</c> : deux
/// habitudes homonymes scinderaient en deux la série de ce qui n'est qu'une intention, alors que
/// deux tâches « Appeler le dentiste » à deux mois d'écart sont bel et bien deux tâches.</para>
/// </summary>
public sealed class CreateTaskCommandHandler(
    IHunterProfileRepository hunterProfiles,
    ITaskItemRepository tasks,
    TimeProvider timeProvider)
    : IRequestHandler<CreateTaskCommand, CreateTaskResult>
{
    public async Task<CreateTaskResult> Handle(
        CreateTaskCommand request, CancellationToken cancellationToken)
    {
        // La clé étrangère refuserait de toute façon l'écriture, mais loin d'ici et dans une
        // langue que le Chasseur n'a pas à lire.
        var profil = await hunterProfiles.GetByIdAsync(request.HunterProfileId, cancellationToken)
            ?? throw new HunterProfileNotFoundException();

        var tache = TaskItem.Create(
            profil.Id, request.Title, request.DueDate, timeProvider.GetUtcNow());

        await tasks.AddAsync(tache, cancellationToken);

        return new CreateTaskResult(tache.Id, tache.Title, tache.DueDate);
    }
}
