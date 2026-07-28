using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Application.Common.Validation;
using Arise.Application.Features.Hunters.Commands.AwardXp;
using Arise.Domain.Hunters;
using MediatR;

namespace Arise.Application.Features.Tasks.Commands.CompleteTask;

/// <summary>
/// Coche une tâche et accorde l'XP d'engagement (doc mécaniques, section 1).
///
/// <para>L'XP passe par <see cref="AwardXpCommand"/> via MediatR, comme partout ailleurs : le
/// moteur de progression a un point d'entrée unique. Aucun événement de domaine n'est publié —
/// la série d'engagement du profil ne se nourrit que de <c>QuestCompletedEvent</c>, et cocher
/// une tâche n'est pas compléter une quête.</para>
///
/// <para>Aucun jeton de concurrence sur la tâche, contrairement à <c>Quest</c> : la garde
/// d'idempotence de l'entité suffit à ce que le gain ne soit accordé qu'une fois par tâche, et
/// deux complétions simultanées de la <b>même</b> tâche écrivent le même instant à quelques
/// millisecondes près.</para>
/// </summary>
public sealed class CompleteTaskCommandHandler(
    ITaskItemRepository tasks,
    IHabitLogRepository habitLogs,
    ISender sender,
    TimeProvider timeProvider)
    : IRequestHandler<CompleteTaskCommand, CompleteTaskResult>
{
    public async Task<CompleteTaskResult> Handle(
        CompleteTaskCommand request, CancellationToken cancellationToken)
    {
        var tache = await tasks.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new TaskNotFoundException();

        // Le rattachement annoncé n'est pas une étiquette de routage : sans ce contrôle,
        // n'importe quel Chasseur cocherait les tâches des autres et s'accorderait leur XP.
        // Même exception que pour une tâche inconnue, pour ne pas révéler celle d'autrui.
        if (tache.HunterProfileId != request.HunterProfileId)
        {
            throw new TaskNotFoundException();
        }

        // Avant de muter : c'est bien « ce qui a déjà été acquis » qu'il faut opposer au plafond,
        // et la tâche en cours n'en fait pas encore partie.
        var dejaAcquis = await XpDEngagementDejaAcquis(request, cancellationToken);

        // La garde d'idempotence est portée par l'entité : deux appels — double-tap, renvoi
        // réseau, deux appareils — ne donnent qu'une complétion, donc qu'un seul gain d'XP.
        if (!tache.Complete(timeProvider.GetUtcNow()))
        {
            return new CompleteTaskResult(
                tache.Id, tache.CompletedAt!.Value, DejaCompletee: true, XpAcquis: 0);
        }

        var gain = BaremeXpEngagement.Accordable(BaremeXpEngagement.PourTache, dejaAcquis);

        // Persister avant d'accorder : si le processus tombait entre les deux, le Chasseur
        // perdrait un gain — pas la garde qui l'empêche d'être accordé deux fois à la reprise.
        await tasks.SaveAsync(tache, cancellationToken);

        if (gain > 0)
        {
            await sender.Send(new AwardXpCommand(tache.HunterProfileId, gain), cancellationToken);
        }

        return new CompleteTaskResult(
            tache.Id, tache.CompletedAt!.Value, DejaCompletee: false, XpAcquis: gain);
    }

    /// <inheritdoc cref="Habits.Commands.LogHabit.LogHabitCommandHandler"/>
    private async Task<int> XpDEngagementDejaAcquis(
        CompleteTaskCommand request, CancellationToken cancellationToken)
    {
        var jour = JourDuChasseur.Aujourdhui(timeProvider, request.FuseauHoraire);

        var habitudesTenues = await habitLogs.GetDayFrequenciesForHunterAsync(
            request.HunterProfileId, jour, cancellationToken);

        var (debut, fin) = JourDuChasseur.FenetreUtc(jour, request.FuseauHoraire);

        var tachesCochees = await tasks.CountCompletedBetweenAsync(
            request.HunterProfileId, debut, fin, cancellationToken);

        return BaremeXpEngagement.TotalDuJour(habitudesTenues, tachesCochees);
    }
}
