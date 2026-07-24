using Arise.Domain.Quests;

namespace Arise.Application.Common.Abstractions;

/// <summary>
/// Accès aux quêtes posées. Implémenté dans la couche Infrastructure : la couche Application
/// ignore qu'il y a un PostgreSQL derrière.
///
/// <para>Vit dans <c>Common/Abstractions</c> et non sous <c>Features/Sport</c> : les quêtes ne
/// sont pas propres au Sport, les quatre domaines poseront les leurs dans la même table.</para>
/// </summary>
public interface IQuestRepository
{
    /// <summary>
    /// La quête de ce Chasseur, dans ce domaine, à cette date — ou <c>null</c> s'il n'y en a
    /// pas encore. <paramref name="questDate"/> est une date déjà exprimée dans le fuseau du
    /// Chasseur : le repository ne convertit rien.
    /// </summary>
    Task<Quest?> GetForDayAsync(
        Guid hunterProfileId,
        QuestDomain domain,
        DateOnly questDate,
        CancellationToken cancellationToken);

    Task SaveAsync(Quest quest, CancellationToken cancellationToken);
}
