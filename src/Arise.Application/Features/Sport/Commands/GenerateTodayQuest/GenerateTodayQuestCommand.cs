using Arise.Application.Common.Messaging;
using Arise.Domain.Quests;

namespace Arise.Application.Features.Sport.Commands.GenerateTodayQuest;

/// <summary>
/// Fait écrire au Système la quête de sport d'un Chasseur pour un jour donné, et la pose.
///
/// <para>La date est portée par la commande plutôt que déduite d'une horloge : le jour du
/// Chasseur dépend de son fuseau, et l'appelant est le seul à le connaître — la requête de
/// lecture aujourd'hui, le <c>briefing-worker</c> de la Phase 4 demain, qui générera la veille
/// au soir pour le lendemain.</para>
/// </summary>
public sealed record GenerateTodayQuestCommand(Guid HunterProfileId, DateOnly QuestDate)
    : ICommand<Quest>;
