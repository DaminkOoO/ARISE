using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Events;
using Arise.Application.Common.Exceptions;
using Arise.Application.Features.Hunters.Commands.AwardXp;
using Arise.Domain.Quests;
using MediatR;

namespace Arise.Application.Features.Sport.Commands.CompleteGymQuest;

/// <summary>
/// Enregistre l'accomplissement d'une quête de sport : marque la quête, la persiste, fait
/// accorder l'XP, et publie le fait de complétion dont la série se nourrit.
///
/// <para>L'XP passe par <see cref="AwardXpCommand"/> via MediatR, jamais par un appel direct à
/// <c>HunterProfile.AwardXp</c> : le moteur de progression a un point d'entrée unique, que les
/// quatre domaines emprunteront, et l'appeler en direct le priverait de sa validation et de son
/// pipeline. Le montant est celui que la quête annonce — le barème a été figé à sa génération,
/// et le recalculer ici ferait que la récompense lue le matin ne serait plus celle accordée le
/// soir.</para>
///
/// <para>La série, elle, ne se met pas à jour ici : elle est l'affaire de
/// <c>StreakUpdateHandler</c>, abonné à l'événement de complétion. Budget, Habitudes et
/// Calendrier publieront le même fait sans recopier cette logique — et ce handler-ci n'a pas à
/// connaître ses abonnés, c'est précisément ce que l'événement sert à éviter.</para>
/// </summary>
public sealed class CompleteGymQuestCommandHandler(
    IQuestRepository quests,
    ISender sender,
    IPublisher publisher,
    TimeProvider timeProvider)
    : IRequestHandler<CompleteGymQuestCommand, CompleteGymQuestResult>
{
    public async Task<CompleteGymQuestResult> Handle(
        CompleteGymQuestCommand request, CancellationToken cancellationToken)
    {
        var quete = await quests.GetByIdAsync(request.QuestId, cancellationToken)
            ?? throw new QuestNotFoundException();

        // Le rattachement annoncé n'est pas une étiquette de routage : sans ce contrôle,
        // n'importe quel Chasseur complèterait les quêtes des autres et s'accorderait leur XP.
        // Même exception que pour une quête inconnue, pour ne pas révéler celle d'autrui.
        if (quete.HunterProfileId != request.HunterProfileId)
        {
            throw new QuestNotFoundException();
        }

        // Une commande de sport ne complète que des quêtes de sport. Dès la Phase 2,
        // l'identifiant d'une quête d'Habitudes passerait le contrôle ci-dessus, serait marqué
        // complété et crédité — en court-circuitant ce que la complétion d'une habitude devra
        // faire de son côté (HabitLog, série d'habitude).
        if (quete.Domain != QuestDomain.Sport)
        {
            throw new QuestNotFoundException();
        }

        // L'instant tel que le Chasseur le vit, comme pour la génération de la quête du jour :
        // c'est de son décalage que l'entité déduit le jour que la série comptera.
        var maintenant = TimeZoneInfo.ConvertTime(
            timeProvider.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById(request.FuseauHoraire));

        // La garde d'idempotence est portée par l'entité : deux appels — double-tap, renvoi
        // réseau, deux appareils — ne donnent qu'une complétion, donc qu'un seul gain d'XP.
        if (!quete.Complete(maintenant))
        {
            return new CompleteGymQuestResult(
                quete.Id, quete.CompletedAt!.Value, DejaCompletee: true, XpGagne: 0);
        }

        // Persister avant d'accorder : si le processus tombait entre les deux, le Chasseur
        // perdrait un gain — pas la garde qui l'empêche d'être accordé deux fois à la reprise.
        // Des deux pannes, c'est la seule qui se rattrape.
        await quests.SaveAsync(quete, cancellationToken);

        await sender.Send(
            new AwardXpCommand(quete.HunterProfileId, quete.XpReward), cancellationToken);

        foreach (var fait in quete.DomainEvents)
        {
            await publisher.Publish(DomainEventNotification.Envelopper(fait), cancellationToken);
        }

        quete.ClearDomainEvents();

        return new CompleteGymQuestResult(
            quete.Id, quete.CompletedAt!.Value, DejaCompletee: false, XpGagne: quete.XpReward);
    }
}
