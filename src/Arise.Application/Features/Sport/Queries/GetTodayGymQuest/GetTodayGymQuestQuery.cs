using Arise.Domain.Quests;
using MediatR;

namespace Arise.Application.Features.Sport.Queries.GetTodayGymQuest;

/// <summary>
/// La quête de sport du jour d'un Chasseur.
/// </summary>
public sealed record GetTodayGymQuestQuery(Guid HunterProfileId, string FuseauHoraire)
    : IRequest<GetTodayGymQuestResult>;

/// <summary>
/// La quête du jour telle que l'écran Sport l'affiche.
/// </summary>
public sealed record GetTodayGymQuestResult(
    Guid QuestId,
    DateOnly QuestDate,
    string Title,
    string Description,
    QuestType Type,
    QuestStat StatTarget,
    QuestDifficulty Difficulty,
    int XpReward,
    bool IsCompleted,
    bool EstRepli);
