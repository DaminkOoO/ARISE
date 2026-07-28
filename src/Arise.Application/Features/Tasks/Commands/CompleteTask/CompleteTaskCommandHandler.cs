using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using MediatR;

namespace Arise.Application.Features.Tasks.Commands.CompleteTask;

/// <summary>
/// Coche une tâche.
///
/// <para><b>Aucun XP accordé, aucun événement publié</b>, pour la même raison que la
/// journalisation d'une habitude : le document de mécaniques ne chiffre de récompense que pour
/// les quêtes, et la série d'engagement du profil se nourrit d'un <c>QuestCompletedEvent</c> que
/// cocher une tâche ne produit pas.</para>
///
/// <para>Aucun jeton de concurrence sur la tâche, contrairement à <c>Quest</c> : deux complétions
/// simultanées écrivent le même instant à quelques millisecondes près, et la dernière l'emporte
/// sans que rien ne soit crédité deux fois. C'est l'XP qui rendait la course dangereuse sur les
/// quêtes ; ici, il n'y en a pas.</para>
/// </summary>
public sealed class CompleteTaskCommandHandler(
    ITaskItemRepository tasks,
    TimeProvider timeProvider)
    : IRequestHandler<CompleteTaskCommand, CompleteTaskResult>
{
    public async Task<CompleteTaskResult> Handle(
        CompleteTaskCommand request, CancellationToken cancellationToken)
    {
        var tache = await tasks.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new TaskNotFoundException();

        // Le rattachement annoncé n'est pas une étiquette de routage : sans ce contrôle,
        // n'importe quel Chasseur cocherait les tâches des autres. Même exception que pour une
        // tâche inconnue, pour ne pas révéler celle d'autrui.
        if (tache.HunterProfileId != request.HunterProfileId)
        {
            throw new TaskNotFoundException();
        }

        // La garde d'idempotence est portée par l'entité : deux appels — double-tap, renvoi
        // réseau, deux appareils — ne donnent qu'une complétion. L'instant rendu reste celui de
        // la première.
        if (!tache.Complete(timeProvider.GetUtcNow()))
        {
            return new CompleteTaskResult(
                tache.Id, tache.CompletedAt!.Value, DejaCompletee: true);
        }

        await tasks.SaveAsync(tache, cancellationToken);

        return new CompleteTaskResult(
            tache.Id, tache.CompletedAt!.Value, DejaCompletee: false);
    }
}
