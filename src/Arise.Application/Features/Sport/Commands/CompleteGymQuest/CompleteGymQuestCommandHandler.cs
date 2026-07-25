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

        // Le jour tel que le Chasseur le vit, comme pour la génération de la quête du jour :
        // c'est lui qui décide si la quête visée est encore accomplissable.
        var aujourdHuiChezLeChasseur = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(
                timeProvider.GetUtcNow(),
                TimeZoneInfo.FindSystemTimeZoneById(request.FuseauHoraire))
                .DateTime);

        // Fenêtre de complétion (doc mécaniques, section 2) : le jour de la quête ou la veille.
        // Sans borne, le Chasseur revenu après dix jours d'absence compléterait les dix quêtes
        // laissées derrière lui — 10 × 20 XP en une minute. Un jour de battement, et pas zéro :
        // le tap arrive parfois après minuit, et un changement de fuseau suffit à faire tourner
        // la date sans que le Chasseur soit en retard.
        if (quete.QuestDate < aujourdHuiChezLeChasseur.AddDays(-1))
        {
            throw new QuestExpiredException();
        }

        // La garde d'idempotence est portée par l'entité : deux appels — double-tap, renvoi
        // réseau, deux appareils — ne donnent qu'une complétion, donc qu'un seul gain d'XP.
        // L'instant n'est qu'un horodatage : le jour que la série comptera est celui de la
        // quête, que l'entité porte déjà.
        if (!quete.Complete(timeProvider.GetUtcNow()))
        {
            return new CompleteGymQuestResult(
                quete.Id, quete.CompletedAt!.Value, DejaCompletee: true, XpAcquis: quete.XpReward);
        }

        try
        {
            // Persister avant d'accorder : si le processus tombait entre les deux, le Chasseur
            // perdrait un gain — pas la garde qui l'empêche d'être accordé deux fois à la
            // reprise. Des deux pannes, c'est la seule qui se rattrape.
            await quests.SaveAsync(quete, cancellationToken);
        }
        // Une complétion simultanée a gagné la course : deux requêtes, deux scopes, deux
        // DbContext, et la garde d'idempotence de l'entité — qui vit en mémoire — n'a rien pu
        // voir. C'est la base qui tranche, et le perdant se comporte alors exactement comme un
        // double-tap séquentiel : l'accomplissement tient, l'XP ne se rejoue pas. Le repository
        // a rafraîchi la quête avec l'état gagnant, dont on annonce l'instant.
        catch (ConcurrentQuestUpdateException)
        {
            return new CompleteGymQuestResult(
                quete.Id, quete.CompletedAt!.Value, DejaCompletee: true, XpAcquis: quete.XpReward);
        }

        await sender.Send(
            new AwardXpCommand(quete.HunterProfileId, quete.XpReward), cancellationToken);

        foreach (var fait in quete.DomainEvents)
        {
            await publisher.Publish(DomainEventNotification.Envelopper(fait), cancellationToken);
        }

        quete.ClearDomainEvents();

        return new CompleteGymQuestResult(
            quete.Id, quete.CompletedAt!.Value, DejaCompletee: false, XpAcquis: quete.XpReward);
    }
}
