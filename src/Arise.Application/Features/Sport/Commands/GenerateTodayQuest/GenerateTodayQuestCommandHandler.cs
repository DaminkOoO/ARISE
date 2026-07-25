using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Domain.Quests;
using MediatR;

namespace Arise.Application.Features.Sport.Commands.GenerateTodayQuest;

/// <summary>
/// Écrit et pose la quête de sport du jour : charge le profil pour en donner le contexte au
/// Système, lui fait rédiger la quête, la pose.
///
/// <para>Rend l'entité posée plutôt qu'un DTO d'écran : la commande sert deux appelants aux
/// besoins d'affichage différents — la requête de lecture, qui la projette pour l'écran Sport,
/// et le <c>briefing-worker</c>, qui n'affichera rien du tout.</para>
/// </summary>
public sealed class GenerateTodayQuestCommandHandler(
    IHunterProfileRepository hunterProfiles,
    IQuestRepository quests,
    IQuestGenerationAgent questGenerationAgent)
    : IRequestHandler<GenerateTodayQuestCommand, Quest>
{
    public async Task<Quest> Handle(
        GenerateTodayQuestCommand request, CancellationToken cancellationToken)
    {
        var profil = await hunterProfiles.GetByIdAsync(request.HunterProfileId, cancellationToken)
            ?? throw new HunterProfileNotFoundException();

        // La commande relit elle-même : elle est envoyée aussi bien par la requête de lecture
        // que, demain, par le briefing-worker, et ne peut pas supposer que son appelant a déjà
        // regardé. C'est aussi ce qui évite de rappeler le Système pour rien.
        var dejaPosee = await quests.GetForDayAsync(
            profil.Id, QuestDomain.Sport, request.QuestDate, cancellationToken);

        // Une quête réellement générée est intouchable : le texte lu le matin est celui qu'on
        // retrouve le soir. Seul un repli se réécrit.
        if (dejaPosee is { IsFallback: false })
        {
            return dejaPosee;
        }

        var generee = await questGenerationAgent.ExecuteAsync(
            new QuestGenerationAgentRequest(profil.Level, profil.Rank, profil.StreakCurrent),
            cancellationToken);

        if (dejaPosee is not null)
        {
            dejaPosee.RegenerateFallback(
                generee.Title,
                generee.Description,
                generee.Type,
                generee.StatTarget,
                generee.Difficulty,
                generee.XpReward,
                generee.EstRepli);

            await quests.SaveAsync(dejaPosee, cancellationToken);

            return dejaPosee;
        }

        var quete = Quest.Generate(
            profil.Id,
            QuestDomain.Sport,
            request.QuestDate,
            generee.Title,
            generee.Description,
            generee.Type,
            generee.StatTarget,
            generee.Difficulty,
            generee.XpReward,
            generee.EstRepli);

        try
        {
            await quests.SaveAsync(quete, cancellationToken);
        }
        // Deux appareils, ou un pull-to-refresh pendant l'appel au Système — qui dure des
        // secondes, la fenêtre est large : l'index unique a tranché, et une quête parfaitement
        // valide existe déjà en base. Le Chasseur n'a aucune raison de voir une erreur pour
        // autant ; on lui rend la gagnante.
        catch (QuestAlreadyPosedException)
        {
            var gagnante = await quests.GetForDayAsync(
                profil.Id, QuestDomain.Sport, request.QuestDate, cancellationToken);

            if (gagnante is null)
            {
                // La collision ne venait donc pas de la quête du jour : la masquer rendrait un
                // résultat vide au Chasseur et cacherait le vrai défaut.
                throw;
            }

            return gagnante;
        }

        return quete;
    }
}
