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

        var generee = await questGenerationAgent.ExecuteAsync(
            new QuestGenerationAgentRequest(profil.Level, profil.Rank, profil.StreakCurrent),
            cancellationToken);

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

        await quests.SaveAsync(quete, cancellationToken);

        return quete;
    }
}
